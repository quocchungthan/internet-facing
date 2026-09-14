"""Local Docker integration tests. No mail is sent to external domains.
Run: python tests/integration.py (after local init, compose up and accounts).
"""
import http.cookiejar
import imaplib
import json
from pathlib import Path
import re
import smtplib
import ssl
import time
import unittest
import urllib.error
import urllib.parse
import urllib.request

ROOT = Path(__file__).resolve().parents[1]
CONFIG = dict(line.split('=', 1) for line in (ROOT / '.env').read_text().splitlines() if line and not line.startswith('#'))
if CONFIG.get('LOCAL_TEST') != '1':
    raise SystemExit('Tests only run with --local configuration.')
ACCOUNTS = json.loads((ROOT / 'secrets/accounts.json').read_text())
DOMAIN = CONFIG['MAIL_DOMAIN']
CTX = ssl.create_default_context(cafile=str(ROOT / 'runtime/ca/test-ca.pem'))
BASE = 'https://localhost:' + CONFIG['HTTPS_PORT']


class Browser:
    def __init__(self):
        self.cookies = http.cookiejar.CookieJar()
        self.opener = urllib.request.build_opener(urllib.request.HTTPSHandler(context=CTX), urllib.request.HTTPCookieProcessor(self.cookies))

    def get(self, path):
        try:
            response = self.opener.open(BASE + path, timeout=40)
        except urllib.error.HTTPError as error:
            response = error
        return response.status, response.read().decode(), response.headers

    def post(self, path, values):
        data = urllib.parse.urlencode(values).encode()
        try:
            response = self.opener.open(BASE + path, data, timeout=40)
        except urllib.error.HTTPError as error:
            response = error
        return response.status, response.read().decode(), response.headers

    def token(self, html):
        return re.search(r'name="csrf" value="([^"]+)"', html).group(1)

    def login(self, name):
        _, html, _ = self.get('/login')
        address = name + '@' + DOMAIN
        return self.post('/login', {'csrf': self.token(html), 'email': address, 'password': ACCOUNTS[address]})


class Integration(unittest.TestCase):
    def test_01_auth_send_receive_sent_and_isolation(self):
        shuneo = Browser()
        status, html, headers = shuneo.login('shuneo')
        self.assertEqual(status, 200, html)
        self.assertIn('Hộp thư đến', html)
        _, form, _ = shuneo.get('/compose')
        subject = 'MVP integration ' + str(time.time_ns())
        status, html, _ = shuneo.post('/compose', {'csrf': shuneo.token(form), 'to': 'admin@' + DOMAIN, 'subject': subject, 'body': 'Xin chào admin!\nTiếng Việt hoạt động. <script>alert(1)</script>'})
        self.assertEqual(status, 200, html)
        self.assertIn(subject, html)
        self.assertIn('Maddy đã nhận thư', html)
        admin = Browser()
        status, inbox, _ = admin.login('admin')
        self.assertEqual(status, 200, inbox)
        self.assertIn(subject, inbox)
        path = re.search(r'href="(/message/[^\"]+)"', inbox).group(1).replace('&amp;', '&')
        status, message, headers = admin.get(path)
        self.assertEqual(status, 200)
        self.assertIn('Tiếng Việt hoạt động.', message)
        self.assertIn('&lt;script&gt;', message)
        self.assertNotIn('<script>', message)
        self.assertIn("script-src 'none'", headers['Content-Security-Policy'])
        _, own_inbox, _ = shuneo.get('/')
        self.assertNotIn(subject, own_inbox)
        status, _, _ = admin.post('/logout', {'csrf': admin.token(message)})
        self.assertEqual(status, 200)
        _, logged_out, _ = admin.get('/')
        self.assertIn('Đăng nhập để đọc', logged_out)

    def test_02_csrf_auth_and_header_injection(self):
        browser = Browser()
        status, html, _ = browser.get('/compose')
        self.assertIn('Đăng nhập để đọc', html)
        status, _, _ = browser.post('/login', {'email': 'admin@' + DOMAIN, 'password': 'wrong'})
        self.assertEqual(status, 400)
        browser.login('shuneo')
        _, form, _ = browser.get('/compose')
        status, _, _ = browser.post('/compose', {'csrf': browser.token(form), 'to': 'admin@' + DOMAIN, 'subject': 'Hello\r\nBcc: victim@example.net', 'body': 'test'})
        self.assertEqual(status, 400)
        status, _, _ = browser.post('/compose', {'csrf': browser.token(form), 'to': 'admin@' + DOMAIN + ',other@example.net', 'subject': 'test', 'body': 'test'})
        self.assertEqual(status, 400)

    def test_03_mail_protocol_security(self):
        with smtplib.SMTP('localhost', int(CONFIG['SUBMISSION_PORT']), timeout=15) as smtp:
            smtp.ehlo()
            self.assertNotIn('auth', smtp.esmtp_features, 'Authentication must require TLS')
            smtp.starttls(context=CTX)
            smtp.ehlo()
            code, _ = smtp.mail('shuneo@' + DOMAIN)
            self.assertGreaterEqual(code, 400, 'Unauthenticated submission accepted')
        with smtplib.SMTP('localhost', int(CONFIG['SUBMISSION_PORT']), timeout=15) as smtp:
            smtp.starttls(context=CTX)
            smtp.login('shuneo@' + DOMAIN, ACCOUNTS['shuneo@' + DOMAIN])
            code, _ = smtp.mail('admin@' + DOMAIN)
            if code < 400:
                code, _ = smtp.rcpt('shuneo@' + DOMAIN)
            if code < 400:
                code, _ = smtp.data('From: admin@' + DOMAIN + '\r\nTo: shuneo@' + DOMAIN + '\r\nSubject: forbidden impersonation\r\n\r\ntest')
            self.assertGreaterEqual(code, 400, 'Sender impersonation was accepted')
        with smtplib.SMTP('localhost', int(CONFIG['SMTP_PORT']), timeout=15) as smtp:
            smtp.ehlo()
            code, _ = smtp.mail('someone@example.org')
            if code < 400:
                code, _ = smtp.rcpt('recipient@example.net')
            self.assertGreaterEqual(code, 400, 'Open relay detected')


if __name__ == '__main__':
    unittest.main(verbosity=2)
