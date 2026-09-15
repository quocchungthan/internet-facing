import os

# Seafile Seahub configuration template for storage.eldervibe.dev
# Location on host: /srv/storage/data/seafile/conf/seahub_settings.py

# ==========================================
# Host & URL Settings
# ==========================================
SERVICE_URL = os.environ.get('SEAFILE_SERVICE_URL', 'https://storage.eldervibe.dev')
FILE_SERVER_ROOT = os.environ.get('SEAFILE_FILE_SERVER_ROOT', 'https://storage.eldervibe.dev/seafhttp')

# ==========================================
# OpenID Connect / OAuth2 SSO with Fences (sub/identity)
# ==========================================
ENABLE_OAUTH = os.environ.get('ENABLE_OAUTH', 'True').lower() in ('true', '1', 't')
OAUTH_ENABLE_INSECURE_URI = os.environ.get('OAUTH_ENABLE_INSECURE_URI', 'False').lower() in ('true', '1', 't')
OAUTH_CLIENT_ID = os.environ.get('OAUTH_CLIENT_ID', 'seafile')
OAUTH_CLIENT_SECRET = os.environ.get('OAUTH_CLIENT_SECRET', '')
OAUTH_REDIRECT_URL = os.environ.get('OAUTH_REDIRECT_URL', 'https://storage.eldervibe.dev/oauth/callback/')
OAUTH_AUTHORIZATION_URL = os.environ.get('OAUTH_AUTHORIZATION_URL', 'https://identity.eldervibe.dev/connect/authorize')
OAUTH_TOKEN_URL = os.environ.get('OAUTH_TOKEN_URL', 'https://identity.eldervibe.dev/connect/token')
OAUTH_USER_INFO_URL = os.environ.get('OAUTH_USER_INFO_URL', 'https://identity.eldervibe.dev/connect/userinfo')
OAUTH_SCOPE = os.environ.get('OAUTH_SCOPE', 'openid profile email').split()
OAUTH_ATTRIBUTE_MAP = {
    "id": (True, "email"),
    "name": (False, "name"),
    "email": (True, "email"),
}

# Auto-activate user upon SSO login
OAUTH_ACTIVATE_USER_AFTER_CREATION = True
OAUTH_CREATE_UNKNOWN_USER = True
