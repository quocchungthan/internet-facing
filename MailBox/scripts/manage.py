#!/usr/bin/env python3
"""Run from any directory. Requires Python 3 and Docker Compose v2."""
import argparse
import datetime
import json
import os
from pathlib import Path
import re
import secrets
import shutil
import subprocess
import sys
import tarfile
import time
import zipfile

ROOT = Path(__file__).resolve().parents[1]
os.chdir(ROOT)


def run(args, **kwargs):
    return subprocess.run(args, check=True, **kwargs)


def compose(*args, **kwargs):
    return run(['docker', 'compose', *args], **kwargs)


def settings():
    if not Path('.env').exists():
        sys.exit('Run init first. See README.md.')
    return dict(line.split('=', 1) for line in Path('.env').read_text().splitlines() if line and not line.startswith('#'))


def init(args):
    if Path('.env').exists():
        sys.exit('.env already exists; refusing to overwrite domain or credentials.')
    domain = args.domain.lower()
    host = (args.hostname or f'mail.{domain}').lower()
    pattern = r'(?=.{1,253}$)(?:[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z]{2,63}'
    if not re.fullmatch(pattern, domain) or not re.fullmatch(pattern, host):
        sys.exit('Invalid domain/hostname. Use an ASCII/punycode domain.')
    for directory in ['runtime/mail', 'runtime/letsencrypt', 'runtime/acme', 'runtime/ca', 'secrets', 'backups']:
        Path(directory).mkdir(parents=True, exist_ok=True)
    config = {'MAIL_DOMAIN': domain, 'MAIL_HOSTNAME': host, 'SECRET_KEY': secrets.token_hex(32)}
    if args.local:
        config.update(BIND_IP='127.0.0.1', SMTP_PORT='2525', SUBMISSION_PORT='1587', IMAP_PORT='1993', HTTP_PORT='8080', HTTPS_PORT='8443', SSL_CERT_FILE='/extra-ca/test-ca.pem', LOCAL_TEST='1')
    accounts = {f'{name}@{domain}': secrets.token_urlsafe(24) for name in ['shuneo', 'admin']}
    Path('.env').write_text(''.join(f'{k}={v}\n' for k, v in config.items()), encoding='utf-8')
    Path('secrets/accounts.json').write_text(json.dumps(accounts, indent=2), encoding='utf-8')
    if os.name != 'nt':
        os.chmod('.env', 0o600)
        os.chmod('secrets', 0o700)
        os.chmod('secrets/accounts.json', 0o600)
    print('Configured domain:', domain)
    print('Credentials saved to secrets/accounts.json (not printed).')
    if args.local:
        local_certificate(host)


def local_certificate(host):
    # Use Certbot image's cryptography dependency; nothing to install on the host.
    code = '''
import datetime, pathlib, sys
from cryptography import x509
from cryptography.x509.oid import NameOID
from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import rsa
host=sys.argv[1]
key=rsa.generate_private_key(public_exponent=65537,key_size=2048)
name=x509.Name([x509.NameAttribute(NameOID.COMMON_NAME,host)])
now=datetime.datetime.now(datetime.timezone.utc)
cert=(x509.CertificateBuilder().subject_name(name).issuer_name(name).public_key(key.public_key()).serial_number(x509.random_serial_number()).not_valid_before(now-datetime.timedelta(minutes=5)).not_valid_after(now+datetime.timedelta(days=14)).add_extension(x509.SubjectAlternativeName([x509.DNSName(host),x509.DNSName('localhost')]),critical=False).add_extension(x509.BasicConstraints(ca=True,path_length=None),critical=True).sign(key,hashes.SHA256()))
directory=pathlib.Path('/runtime/letsencrypt/live')/host
directory.mkdir(parents=True,exist_ok=True)
(directory/'privkey.pem').write_bytes(key.private_bytes(serialization.Encoding.PEM,serialization.PrivateFormat.PKCS8,serialization.NoEncryption()))
(directory/'privkey.pem').chmod(0o600)
data=cert.public_bytes(serialization.Encoding.PEM)
(directory/'fullchain.pem').write_bytes(data)
pathlib.Path('/runtime/ca/test-ca.pem').write_bytes(data)
'''
    run(['docker', 'run', '--rm', '-v', f'{ROOT / "runtime"}:/runtime', '--entrypoint', 'python', 'certbot/certbot:v4.2.0', '-c', code, host])
    print('Local-only certificate created. Do not deploy this runtime to a VPS.')


def certificate(args):
    config = settings()
    if config.get('LOCAL_TEST'):
        sys.exit('Local configuration; create a fresh production directory.')
    if not args.email:
        sys.exit('--email is required for Let\'s Encrypt registration.')
    compose('run', '--rm', '--service-ports', 'certbot', 'certonly', '--standalone', '--non-interactive', '--agree-tos', '--email', args.email, '-d', config['MAIL_HOSTNAME'])


def accounts(args):
    config = settings()
    # CLI commands use the same mounted configuration/database as the server.
    prefix = ['exec', '-T', 'maddy', 'maddy', '-config', '/data/maddy.conf']
    for attempt in range(30):
        result = subprocess.run(['docker', 'compose', *prefix, 'creds', 'list'], capture_output=True, text=True)
        if result.returncode == 0:
            break
        time.sleep(2)
    else:
        sys.exit('Maddy unavailable. Inspect: docker compose logs maddy')
    credentials = json.loads(Path('secrets/accounts.json').read_text())
    existing = result.stdout
    storage = compose(*prefix, 'imap-acct', 'list', capture_output=True, text=True).stdout
    for address, password in credentials.items():
        if address not in existing.split():
            compose(*prefix, 'creds', 'create', address, input=password + '\n', text=True, stdout=subprocess.DEVNULL)
        if address not in storage.split():
            compose(*prefix, 'imap-acct', 'create', address)
        print('Account ready:', address)
    print('Passwords: secrets/accounts.json. Existing passwords are never reset by this command.')


def renew(args):
    if settings().get('LOCAL_TEST'):
        sys.exit('Renewal is for production certificates only.')
    compose('run', '--rm', 'certbot', 'renew', '--webroot', '-w', '/var/www/acme', *(['--dry-run'] if args.dry_run else []))
    if not args.dry_run:
        compose('exec', '-T', 'proxy', 'nginx', '-s', 'reload')
        # Maddy reloads certificate files automatically; restart also guarantees pickup.
        compose('restart', 'maddy')


def backup(args):
    settings()
    Path('backups').mkdir(exist_ok=True)
    target = ROOT / 'backups' / (datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%SZ') + '.tar.gz')
    compose('stop', 'proxy', 'web', 'maddy')
    try:
        with tarfile.open(target, 'w:gz') as archive:
            for name in ['runtime', '.env', 'secrets', 'maddy', 'compose.yaml']:
                archive.add(name, arcname=name)
        if os.name != 'nt':
            os.chmod(target, 0o600)
    finally:
        compose('up', '-d')
    print('Backup contains private keys and passwords; encrypt before off-site transfer:', target)


def package(args):
    Path('dist').mkdir(exist_ok=True)
    target = Path('dist/shuneo-mail-mvp.zip')
    excluded = {'.git', '.venv', 'runtime', 'secrets', 'backups', 'dist', '__pycache__', '.pytest_cache'}
    with zipfile.ZipFile(target, 'w', zipfile.ZIP_DEFLATED) as archive:
        for path in ROOT.rglob('*'):
            relative = path.relative_to(ROOT)
            if path.is_file() and not excluded.intersection(relative.parts) and relative.name != '.env':
                archive.write(path, Path('shuneo-mail-mvp') / relative)
    print('Deployment source package (no passwords, certificates or mail):', target.resolve())


parser = argparse.ArgumentParser(description=__doc__)
subs = parser.add_subparsers(dest='command', required=True)
p = subs.add_parser('init')
p.add_argument('--domain', required=True)
p.add_argument('--hostname')
p.add_argument('--local', action='store_true')
p = subs.add_parser('certificate')
p.add_argument('--email')
subs.add_parser('accounts')
p = subs.add_parser('renew')
p.add_argument('--dry-run', action='store_true')
subs.add_parser('backup')
subs.add_parser('package')
args = parser.parse_args()
try:
    globals()[args.command](args)
except subprocess.CalledProcessError as exc:
    sys.exit(f'Command failed (exit {exc.returncode}). Fix the reported error before continuing.')
