import imaplib
import json
import os
import re
import secrets
import smtplib
import ssl
import threading
import time
import urllib.parse
import urllib.request
from contextlib import contextmanager
from email import policy
from email.parser import BytesParser
from email.message import EmailMessage
from email.utils import formatdate, make_msgid
from html.parser import HTMLParser
from pathlib import Path

from flask import Flask, abort, flash, redirect, render_template, request, session, url_for

app = Flask(__name__)
app.config.update(
    SECRET_KEY=os.environ['SECRET_KEY'],
    SESSION_COOKIE_SECURE=True,
    SESSION_COOKIE_HTTPONLY=True,
    SESSION_COOKIE_SAMESITE='Lax',
    MAX_CONTENT_LENGTH=1024 * 1024,
    PERMANENT_SESSION_LIFETIME=3600,
)
DOMAIN = os.environ['MAIL_DOMAIN']
HOST = os.environ['MAIL_HOSTNAME']
FOLDERS = {'inbox': 'INBOX', 'sent': 'Sent'}
sessions = {}
lock = threading.RLock()
ADDRESS = re.compile(r"[A-Za-z0-9.!#$%&'*+/=?^_`{|}~-]+@[A-Za-z0-9](?:[A-Za-z0-9.-]*[A-Za-z0-9])?\.[A-Za-z]{2,63}\Z")

# Management login: GitHub via Fences (identity.eldervibe.dev); mailbox access is granted
# per GitHub account through the hardcoded secrets/managed_accounts.json map, not a password form.
OAUTH_ISSUER = os.environ.get('OAUTH_ISSUER', 'https://identity.eldervibe.dev').rstrip('/')
OAUTH_CLIENT_ID = os.environ.get('OAUTH_CLIENT_ID', '')
OAUTH_CLIENT_SECRET = os.environ.get('OAUTH_CLIENT_SECRET', '')
OAUTH_REDIRECT_URL = os.environ.get('OAUTH_REDIRECT_URL', f'https://{HOST}/auth/github/callback')
MANAGED_ACCOUNTS_PATH = Path('secrets/managed_accounts.json')
ACCOUNTS_PATH = Path('secrets/accounts.json')


def oauth_configured():
    return bool(OAUTH_CLIENT_ID and OAUTH_CLIENT_SECRET)


def load_json_map(path):
    try:
        return json.loads(path.read_text(encoding='utf-8'))
    except (FileNotFoundError, json.JSONDecodeError):
        return {}


def managed_accounts_for(github_email):
    mapping = {str(k).strip().lower(): [str(v).strip().lower() for v in values] for k, values in load_json_map(MANAGED_ACCOUNTS_PATH).items()}
    return mapping.get(github_email.strip().lower(), [])


def tls_context():
    return ssl.create_default_context()


@contextmanager
def mailbox(email, password):
    client = imaplib.IMAP4_SSL(HOST, 993, ssl_context=tls_context(), timeout=15)
    try:
        client.login(email, password)
        yield client
    finally:
        try:
            client.logout()
        except Exception:
            pass


def checked(result):
    status, data = result
    if status != 'OK':
        raise imaplib.IMAP4.error('Mailbox operation failed')
    return data


def current_user():
    now = time.time()
    with lock:
        for key in list(sessions):
            if sessions[key]['expires'] <= now:
                del sessions[key]
        return sessions.get(session.get('sid'))


def csrf():
    if 'csrf' not in session:
        session['csrf'] = secrets.token_urlsafe(32)
    return session['csrf']


app.jinja_env.globals.update(csrf=csrf, domain=DOMAIN)


@app.before_request
def protect():
    if request.method == 'POST':
        expected = session.get('csrf', '')
        supplied = request.form.get('csrf', '')
        if not expected or not secrets.compare_digest(expected, supplied):
            abort(400, 'Phiên biểu mẫu hết hạn. Hãy tải lại trang.')
    if request.endpoint not in {'login', 'static', 'health', 'auth_github', 'auth_github_callback', 'choose_mailbox', 'auth_select'} and not current_user():
        return redirect(url_for('login'))


@app.after_request
def headers(response):
    response.headers['Content-Security-Policy'] = "default-src 'self'; script-src 'none'; style-src 'self'; img-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'"
    response.headers['X-Content-Type-Options'] = 'nosniff'
    response.headers['X-Frame-Options'] = 'DENY'
    response.headers['Referrer-Policy'] = 'no-referrer'
    response.headers['Cache-Control'] = 'no-store'
    return response


@app.get('/healthz')
def health():
    return {'status': 'ok'}


@app.get('/login')
def login():
    return render_template('login.html', domain=DOMAIN, oauth_configured=oauth_configured())


@app.get('/auth/github')
def auth_github():
    if not oauth_configured():
        abort(503, 'GitHub login is not configured.')
    state = secrets.token_urlsafe(32)
    session['oauth_state'] = state
    params = urllib.parse.urlencode({
        'response_type': 'code',
        'client_id': OAUTH_CLIENT_ID,
        'redirect_uri': OAUTH_REDIRECT_URL,
        'scope': 'openid profile email',
        'state': state,
    })
    return redirect(f'{OAUTH_ISSUER}/connect/authorize?{params}')


@app.get('/auth/github/callback')
def auth_github_callback():
    if not oauth_configured():
        abort(503, 'GitHub login is not configured.')
    state = request.args.get('state', '')
    if not state or not secrets.compare_digest(state, session.pop('oauth_state', '')):
        flash('Phiên đăng nhập GitHub hết hạn. Hãy thử lại.', 'error')
        return redirect(url_for('login'))
    code = request.args.get('code', '')
    if not code:
        flash('Đăng nhập GitHub bị hủy.', 'error')
        return redirect(url_for('login'))
    try:
        token_body = urllib.parse.urlencode({
            'grant_type': 'authorization_code',
            'code': code,
            'redirect_uri': OAUTH_REDIRECT_URL,
            'client_id': OAUTH_CLIENT_ID,
            'client_secret': OAUTH_CLIENT_SECRET,
        }).encode('ascii')
        token_request = urllib.request.Request(
            f'{OAUTH_ISSUER}/connect/token', data=token_body, method='POST',
            headers={'Content-Type': 'application/x-www-form-urlencoded'})
        with urllib.request.urlopen(token_request, timeout=10) as response:
            token = json.loads(response.read())
        userinfo_request = urllib.request.Request(
            f'{OAUTH_ISSUER}/connect/userinfo',
            headers={'Authorization': f'Bearer {token["access_token"]}'})
        with urllib.request.urlopen(userinfo_request, timeout=10) as response:
            userinfo = json.loads(response.read())
    except Exception:
        flash('Không thể xác thực với GitHub. Hãy thử lại.', 'error')
        return redirect(url_for('login'))
    managed = managed_accounts_for(str(userinfo.get('email', '')))
    if not managed:
        flash('Tài khoản GitHub này chưa được cấp quyền quản lý hộp thư nào.', 'error')
        return redirect(url_for('login'))
    if len(managed) == 1:
        return select_mailbox(managed[0])
    session['manager_accounts'] = managed
    return redirect(url_for('choose_mailbox'))


@app.get('/auth/choose')
def choose_mailbox():
    managed = session.get('manager_accounts') or []
    if not managed:
        return redirect(url_for('login'))
    return render_template('choose.html', domain=DOMAIN, accounts=managed)


@app.post('/auth/select')
def auth_select():
    managed = session.get('manager_accounts') or []
    address = request.form.get('address', '').strip().lower()
    if address not in managed:
        abort(403)
    return select_mailbox(address)


def select_mailbox(address):
    password = load_json_map(ACCOUNTS_PATH).get(address)
    if not password:
        flash('Không tìm thấy thông tin đăng nhập hộp thư này.', 'error')
        return redirect(url_for('login'))
    try:
        with mailbox(address, password):
            pass
    except imaplib.IMAP4.error:
        flash('Hộp thư từ chối thông tin đăng nhập đã lưu. Liên hệ quản trị viên.', 'error')
        return redirect(url_for('login'))
    except (OSError, ssl.SSLError):
        flash('Chưa kết nối được máy chủ mail. Kiểm tra dịch vụ và TLS.', 'error')
        return redirect(url_for('login'))
    now = time.time()
    with lock:
        sessions.pop(session.get('sid'), None)
        existing = [key for key, value in sessions.items() if value['email'] == address]
        for key in existing[:-4]:
            sessions.pop(key, None)
        sid = secrets.token_urlsafe(32)
        sessions[sid] = {'email': address, 'password': password, 'expires': now + 3600, 'sends': []}
    session.pop('manager_accounts', None)
    session['sid'] = sid
    session.permanent = True
    return redirect(url_for('inbox'))


@app.post('/logout')
def logout():
    with lock:
        sessions.pop(session.get('sid'), None)
    session.clear()
    return redirect(url_for('login'))


@app.get('/')
def inbox():
    user = current_user()
    folder = request.args.get('folder', 'inbox')
    if folder not in FOLDERS:
        abort(400)
    page = max(1, request.args.get('page', 1, type=int))
    messages = []
    with mailbox(user['email'], user['password']) as client:
        if folder == 'sent':
            client.create('Sent')
        checked(client.select(FOLDERS[folder], readonly=True))
        ids = checked(client.uid('search', None, 'ALL'))[0].split()[::-1]
        for uid in ids[(page - 1) * 20:page * 20]:
            data = checked(client.uid('fetch', uid, '(FLAGS BODY.PEEK[HEADER.FIELDS (FROM TO SUBJECT DATE)])'))
            item = next((x for x in data if isinstance(x, tuple)), None)
            if not item:
                continue
            msg = BytesParser(policy=policy.default).parsebytes(item[1])
            messages.append({'uid': uid.decode(), 'subject': str(msg.get('Subject', '(Không có tiêu đề)')),
                             'sender': str(msg.get('From', '')), 'to': str(msg.get('To', '')),
                             'date': str(msg.get('Date', '')), 'unread': b'\\Seen' not in item[0]})
    return render_template('inbox.html', user=user, messages=messages, folder=folder, page=page, more=page * 20 < len(ids))


class PlainHTML(HTMLParser):
    def __init__(self):
        super().__init__()
        self.parts = []
        self.hidden = 0

    def handle_starttag(self, tag, attrs):
        if tag in {'script', 'style'}:
            self.hidden += 1
        if tag in {'br', 'p', 'div', 'tr', 'li'}:
            self.parts.append('\n')

    def handle_endtag(self, tag):
        if tag in {'script', 'style'}:
            self.hidden = max(0, self.hidden - 1)

    def handle_data(self, data):
        if not self.hidden:
            self.parts.append(data)


def message_text(msg):
    part = msg.get_body(preferencelist=('plain', 'html'))
    if part is None:
        return '(Thư không có nội dung văn bản hỗ trợ.)'
    try:
        text = part.get_content()
    except (LookupError, UnicodeError):
        text = (part.get_payload(decode=True) or b'').decode('utf-8', errors='replace')
    if part.get_content_type() == 'text/html':
        parser = PlainHTML()
        parser.feed(text)
        text = ''.join(parser.parts)
    return text


@app.get('/message/<int:uid>')
def read_message(uid):
    user = current_user()
    folder = request.args.get('folder', 'inbox')
    if folder not in FOLDERS or uid < 1:
        abort(400)
    with mailbox(user['email'], user['password']) as client:
        checked(client.select(FOLDERS[folder], readonly=True))
        # Fetch size before downloading to bound memory usage for incoming mail.
        meta = checked(client.uid('fetch', str(uid), '(RFC822.SIZE)'))
        rawmeta = b' '.join(x for x in meta if isinstance(x, bytes))
        match = re.search(rb'RFC822.SIZE (\d+)', rawmeta)
        if not match:
            abort(404)
        if int(match[1]) > 10 * 1024 * 1024:
            flash('Thư lớn hơn 10 MB. Hãy mở bằng ứng dụng IMAP trên máy tính.', 'error')
            return redirect(url_for('inbox', folder=folder))
        data = checked(client.uid('fetch', str(uid), '(BODY.PEEK[])'))
        item = next((x for x in data if isinstance(x, tuple)), None)
        if not item:
            abort(404)
        msg = BytesParser(policy=policy.default).parsebytes(item[1])
    attachments = [str(p.get_filename()) for p in msg.walk() if p.get_filename()]
    return render_template('message.html', user=user, folder=folder, msg=msg, body=message_text(msg), attachments=attachments)


@app.route('/compose', methods=['GET', 'POST'])
def compose():
    user = current_user()
    if request.method == 'POST':
        recipient = request.form.get('to', '').strip()
        subject = request.form.get('subject', '')
        body = request.form.get('body', '')
        if not ADDRESS.fullmatch(recipient) or len(recipient) > 254 or '\r' in subject or '\n' in subject or len(subject) > 200 or len(body) > 100000:
            flash('Nhập một email người nhận hợp lệ; tiêu đề tối đa 200 và nội dung tối đa 100.000 ký tự.', 'error')
            return render_template('compose.html', user=user), 400
        with lock:
            now = time.time()
            user['sends'] = [t for t in user['sends'] if now - t < 60]
            if len(user['sends']) >= 5:
                flash('Tối đa 5 lần gửi mỗi phút cho phiên này.', 'error')
                return render_template('compose.html', user=user), 429
            user['sends'].append(now)
        msg = EmailMessage()
        msg['From'] = user['email']
        msg['To'] = recipient
        msg['Subject'] = subject
        msg['Date'] = formatdate(localtime=False)
        msg['Message-ID'] = make_msgid(domain=DOMAIN)
        msg.set_content(body)
        # Ensure Sent is available before sending, but do not append until accepted.
        with mailbox(user['email'], user['password']) as client:
            client.create('Sent')
            checked(client.select('Sent'))
        try:
            with smtplib.SMTP(HOST, 587, timeout=20) as smtp:
                smtp.ehlo()
                smtp.starttls(context=tls_context())
                smtp.ehlo()
                smtp.login(user['email'], user['password'])
                smtp.send_message(msg)
        except (smtplib.SMTPException, OSError):
            flash('Không xác nhận được việc gửi. Kiểm tra log/hộp thư trước khi gửi lại để tránh trùng thư.', 'error')
            return render_template('compose.html', user=user), 502
        try:
            with mailbox(user['email'], user['password']) as client:
                checked(client.append('Sent', '\\Seen', imaplib.Time2Internaldate(time.time()), msg.as_bytes(policy=policy.SMTP)))
            flash('Maddy đã nhận thư để chuyển đi. Điều này chưa xác nhận thư vào Inbox của người nhận.', 'success')
        except (imaplib.IMAP4.error, OSError):
            flash('Maddy đã nhận thư, nhưng lưu bản sao vào Đã gửi thất bại. Không gửi lại thư này.', 'error')
        return redirect(url_for('inbox', folder='sent'))
    return render_template('compose.html', user=user)


@app.errorhandler(imaplib.IMAP4.error)
@app.errorhandler(OSError)
def mail_error(error):
    app.logger.warning('Mail connection failed: %s', type(error).__name__)
    return render_template('error.html', user=current_user()), 503
