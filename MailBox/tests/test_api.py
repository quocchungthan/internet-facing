import importlib.util
import os
import secrets
from pathlib import Path
import smtplib
import sys
import tempfile
import time
import unittest
from unittest.mock import MagicMock, patch

os.environ.update({'SECRET_KEY': secrets.token_hex(32), 'MAIL_DOMAIN': 'example.invalid',
                   'MAIL_HOSTNAME': 'mail.example.invalid'})
spec = importlib.util.spec_from_file_location('mail_app', Path(__file__).parents[1] / 'web' / 'app.py')
mail = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = mail
spec.loader.exec_module(mail)


class MailApiTests(unittest.TestCase):
    def setUp(self):
        mail.app.config['TESTING'] = True
        mail.sessions.clear()
        self.client = mail.app.test_client()
        self.base = {'base_url': 'https://mail.example.invalid'}
        self.csrf = self.client.get('/api/mail/session', **self.base).json['csrf']

    def sign_in(self):
        mail.sessions['test-session'] = {
            'email': 'owner@example.invalid', 'password': 'test-only',
            'expires': time.time() + 3600, 'sends': []}
        with self.client.session_transaction(base_url=self.base['base_url']) as session:
            session['sid'] = 'test-session'

    def post(self, path='/api/mail/compose', **data):
        return self.client.post(path, data={'csrf': self.csrf, **data}, **self.base)

    def test_session_and_auth_errors_are_json_without_credentials(self):
        response = self.client.get('/api/mail/session', **self.base)
        self.assertIsNone(response.json['email'])
        self.assertEqual(set(response.json), {'email', 'domain', 'csrf', 'oauthConfigured', 'mailboxes', 'authErrors'})
        for path in ['/api/mail/messages', '/api/mail/messages/1']:
            response = self.client.get(path, **self.base)
            self.assertEqual(response.status_code, 401)
            self.assertTrue(response.is_json)
            self.assertNotIn('Location', response.headers)
        self.sign_in()
        self.assertEqual(self.client.get('/api/mail/session', **self.base).json['email'], 'owner@example.invalid')

    def test_folder_page_csrf_and_origin_validation(self):
        self.sign_in()
        for query in ['folder=other', 'page=0', 'page=-1', 'page=abc', 'page=1.5', 'page=9999999999']:
            self.assertEqual(self.client.get('/api/mail/messages?' + query, **self.base).status_code, 400)
        self.assertEqual(self.client.post('/api/mail/compose', **self.base).status_code, 400)
        response = self.client.post('/api/mail/compose', data={'csrf': self.csrf},
                                    headers={'Origin': 'https://other.invalid'}, **self.base)
        self.assertEqual(response.status_code, 403)

    def test_list_is_bounded_folder_scoped_and_does_not_fetch_bodies(self):
        self.sign_in()
        imap = MagicMock()
        imap.select.return_value = ('OK', [])
        def uid(command, *args):
            if command == 'search':
                return ('OK', [b' '.join(str(i).encode() for i in range(1, 23))])
            return ('OK', [(b'FLAGS ()', b'Subject: Test\r\nFrom: sender@example.invalid\r\n\r\n')])
        imap.uid.side_effect = uid
        with patch.object(mail, 'mailbox') as mailbox:
            mailbox.return_value.__enter__.return_value = imap
            response = self.client.get('/api/mail/messages?folder=sent&page=1', **self.base)
        self.assertEqual(response.status_code, 200)
        self.assertEqual(len(response.json['messages']), 20)
        self.assertEqual(response.json['messages'][0]['uid'], 22)
        self.assertTrue(response.json['more'])
        imap.select.assert_called_once_with('Sent', readonly=True)
        self.assertTrue(all('HEADER.FIELDS' in call.args[-1] for call in imap.uid.call_args_list if call.args[0] == 'fetch'))

    def test_read_keeps_plain_text_and_size_limit(self):
        self.sign_in()
        imap = MagicMock()
        imap.select.return_value = ('OK', [])
        imap.uid.side_effect = [
            ('OK', [b'1 (RFC822.SIZE 100)']),
            ('OK', [(b'FLAGS ()', b'Subject: Read\r\nContent-Type: text/html\r\n\r\n<p>Hello</p><script>bad()</script>')])]
        with patch.object(mail, 'mailbox') as mailbox:
            mailbox.return_value.__enter__.return_value = imap
            response = self.client.get('/api/mail/messages/1?folder=inbox', **self.base)
            self.assertEqual(response.status_code, 200)
            self.assertIn('Hello', response.json['body'])
            self.assertNotIn('bad()', response.json['body'])
            self.assertTrue(response.json['unread'])
            imap.uid.side_effect = [('OK', [b'1 (RFC822.SIZE 10485761)'])]
            self.assertEqual(self.client.get('/api/mail/messages/1', **self.base).status_code, 413)

    def test_send_acceptance_partial_success_uncertainty_and_rate_limit(self):
        self.sign_in()
        with patch.object(mail, 'mailbox') as mailbox, patch.object(mail.smtplib, 'SMTP') as smtp:
            imap = mailbox.return_value.__enter__.return_value
            imap.select.return_value = ('OK', [])
            imap.append.return_value = ('OK', [])
            draft = {'to': 'friend@example.invalid', 'subject': 'Hello', 'body': 'Text'}
            self.assertEqual(self.post(**draft).json, {'accepted': True, 'saved': True})
            imap.append.return_value = ('NO', [])
            self.assertEqual(self.post(**draft).json, {'accepted': True, 'saved': False})
            imap.append.reset_mock()
            smtp.return_value.__enter__.return_value.send_message.side_effect = smtplib.SMTPServerDisconnected('test')
            uncertain = self.post(**draft)
            self.assertEqual(uncertain.status_code, 502)
            self.assertIn('uncertain', uncertain.json['detail'])
            imap.append.assert_not_called()
            mail.sessions['test-session']['sends'] = [time.time()] * 5
            self.assertEqual(self.post(**draft).status_code, 429)
            self.assertEqual(self.post(to='two@example.invalid,other@example.invalid').status_code, 400)
            self.assertEqual(self.post(to=draft['to'], subject='Header\r\nInjection').status_code, 400)

    def test_unavailable_is_json_and_logout_revokes_session(self):
        self.sign_in()
        with patch.object(mail, 'mailbox', side_effect=OSError('private upstream detail')):
            response = self.client.get('/api/mail/messages', **self.base)
        self.assertEqual(response.status_code, 503)
        self.assertNotIn('private upstream', response.get_data(as_text=True))
        self.assertEqual(self.post('/api/mail/logout').json, {'signedOut': True})
        self.assertFalse(mail.sessions)
        self.assertIsNone(self.client.get('/api/mail/session', **self.base).json['email'])

    def test_frontend_paths_and_auth_entries_share_nonce_policy(self):
        with tempfile.TemporaryDirectory() as directory, patch.object(mail, 'FRONTEND_PATH', Path(directory)):
            Path(directory, 'index.html').write_text('<base href="/"><app-root></app-root>', encoding='utf-8')
            Path(directory, 'main.js').write_text('export {};', encoding='utf-8')
            for path in ['/', '/compose', '/message/42?folder=sent', '/login']:
                response = self.client.get(path, **self.base)
                self.assertEqual(response.status_code, 200)
                self.assertIn('ngCspNonce=', response.get_data(as_text=True))
                self.assertIn("script-src 'self'", response.headers['Content-Security-Policy'])
                self.assertNotIn("'unsafe-inline'", response.headers['Content-Security-Policy'])
            with self.client.get('/main.js', **self.base) as asset:
                self.assertEqual(asset.status_code, 200)
            self.assertEqual(self.client.get('/api/missing', **self.base).status_code, 404)
            self.assertEqual(self.client.get('/app.py', **self.base).status_code, 404)
            self.assertEqual(self.client.get('/auth/choose', **self.base).status_code, 302)
            with self.client.session_transaction(base_url=self.base['base_url']) as session:
                session['manager_accounts'] = ['owner@example.invalid', 'other@example.invalid']
            choose = self.client.get('/auth/choose', **self.base)
            self.assertEqual(choose.status_code, 200)
            self.assertIn('ngCspNonce=', choose.get_data(as_text=True))
            Path(directory, 'index.html').unlink()
            self.assertEqual(self.client.get('/', **self.base).status_code, 503)

    def test_chooser_exposes_only_authorized_addresses_and_preserves_server_selection(self):
        accounts = ['owner@example.invalid', 'other@example.invalid']
        with self.client.session_transaction(base_url=self.base['base_url']) as session:
            session['manager_accounts'] = accounts
            session['_flashes'] = [('error', 'An explicit authentication failure')]
        details = self.client.get('/api/mail/session', **self.base).json
        self.assertEqual(details['mailboxes'], accounts)
        self.assertEqual(details['authErrors'], ['An explicit authentication failure'])
        self.assertEqual(self.client.get('/api/mail/session', **self.base).json['authErrors'], [])
        self.assertEqual(self.post('/auth/select', address='unauthorized@example.invalid').status_code, 403)
        self.assertEqual(self.client.post('/auth/select', data={'address': accounts[0]}, **self.base).status_code, 400)
        with patch.object(mail, 'load_json_map', return_value={accounts[0]: 'test-only-password'}), patch.object(mail, 'mailbox'):
            response = self.post('/auth/select', address=accounts[0])
        self.assertEqual(response.status_code, 302)
        self.assertEqual(response.headers['Location'], '/')
        selected = self.client.get('/api/mail/session', **self.base).json
        self.assertEqual(selected['email'], accounts[0])
        self.assertEqual(selected['mailboxes'], [])
        self.assertNotIn('test-only-password', str(selected))


if __name__ == '__main__':
    unittest.main()
