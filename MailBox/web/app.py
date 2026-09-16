import imaplib
import os
import re
import secrets
import smtplib
import ssl
import threading
import time
from contextlib import contextmanager
from email import policy
from email.parser import BytesParser
from email.message import EmailMessage
from email.utils import formatdate, make_msgid
from html.parser import HTMLParser

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
ALLOWED = {f'shuneo@{DOMAIN}', f'admin@{DOMAIN}'}
FOLDERS = {'inbox': 'INBOX', 'sent': 'Sent'}
sessions = {}
attempts = {}
lock = threading.RLock()
ADDRESS = re.compile(r"[A-Za-z0-9.!#$%&'*+/=?^_`{|}~-]+@[A-Za-z0-9](?:[A-Za-z0-9.-]*[A-Za-z0-9])?\.[A-Za-z]{2,63}\Z")


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
    if request.endpoint not in {'login', 'static', 'health'} and not current_user():
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


@app.route('/login', methods=['GET', 'POST'])
def login():
    if request.method == 'POST':
        email = request.form.get('email', '').strip().lower()
        password = request.form.get('password', '')
        if email not in ALLOWED or len(password) > 256:
            flash('Email hoặc mật khẩu không đúng.', 'error')
            return render_template('login.html', domain=DOMAIN), 401
        now = time.time()
        with lock:
            history = [t for t in attempts.get(email, []) if now - t < 300]
            if len(history) >= 10:
                flash('Quá nhiều lần đăng nhập. Hãy thử lại sau 5 phút.', 'error')
                return render_template('login.html', domain=DOMAIN), 429
            attempts[email] = history + [now]
        try:
            with mailbox(email, password):
                pass
        except imaplib.IMAP4.error:
            flash('Email hoặc mật khẩu không đúng.', 'error')
            return render_template('login.html', domain=DOMAIN), 401
        except (OSError, ssl.SSLError):
            flash('Chưa kết nối được máy chủ mail. Kiểm tra dịch vụ và TLS.', 'error')
            return render_template('login.html', domain=DOMAIN), 503
        with lock:
            attempts.pop(email, None)
            sessions.pop(session.get('sid'), None)
            # Limit concurrent sessions per account; credentials live only in server RAM.
            existing = [key for key, value in sessions.items() if value['email'] == email]
            for key in existing[:-4]:
                sessions.pop(key, None)
            sid = secrets.token_urlsafe(32)
            sessions[sid] = {'email': email, 'password': password, 'expires': now + 3600, 'sends': []}
        session.clear()
        session['sid'] = sid
        session.permanent = True
        return redirect(url_for('inbox'))
    return render_template('login.html', domain=DOMAIN)


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
