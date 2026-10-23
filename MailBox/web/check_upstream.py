import imaplib
import smtplib


def check_upstream(host, context):
    with imaplib.IMAP4_SSL(host, 993, ssl_context=context, timeout=10) as client:
        if client.noop()[0] != 'OK':
            raise RuntimeError('Configured IMAP upstream rejected its protocol check.')
    with smtplib.SMTP(host, 587, timeout=10) as client:
        client.ehlo()
        client.starttls(context=context)
        if client.ehlo()[0] != 250:
            raise RuntimeError('Configured SMTP upstream rejected its TLS protocol check.')


if __name__ == '__main__':
    from app import HOST, tls_context
    check_upstream(HOST, tls_context())
    print('Configured IMAP and SMTP upstreams passed TLS/protocol checks; mailbox login and delivery are not verified.')
