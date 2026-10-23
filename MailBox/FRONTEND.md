# SingleUI frontend and Mail API

The `sub/mail` migration replaces native inbox/read/compose templates with the
verified `app-mail` static bundle. Flask remains the API/auth host; Maddy remains
the IMAP/SMTP/storage owner. No unrelated AI capability was removed.

Pin a successful main-branch **SingleUI apps and skins** run from
`quocchungthan/EMFc-experiments`. Set `SINGLE_UI_RUN_ID` and
`SINGLE_UI_REVISION` (full source SHA) as repository/environment variables.
`SINGLE_UI_TOKEN` is a server-only secret with Actions:read and Contents:read
on that repository. The deploy job checks out the pinned importer, downloads
**single-ui-apps**, validates app/revision/hashes and stages `mail` under
`MailBox/web/frontend/`. The Docker build fails explicitly without it.
Artifacts expire after 90 days; retain an operator copy for rollback.

For local use, install Node 22 and authenticated `gh`, check out the producer at
the pinned SHA, then run its `SingleUI/deployment/artifacts/import-app.mjs` with
arguments `mail` and the absolute empty `MailBox/web/frontend` destination.
Develop the Angular UI separately without Docker in SingleUI.

Existing frontend URLs `/`, `/?folder=sent&page=N`, `/compose`,
`/message/UID?folder=...`, `/login` and `/auth/choose` serve Angular.
`/auth/github`, callback and `/auth/select` remain server-owned with the existing
managed-account authorization and session-bound CSRF. The chooser displays only
addresses from the authenticated server session and posts the original form
contract; it does not receive mailbox passwords. OAuth failure messages remain
visible in the shared entry. Angular entries use self-script/nonce-style CSP;
native error pages retain script-src none. No blanket CSP disable or credentialed wildcard CORS.
Frontend/API releases build and recreate only `web`, with `--no-deps`; they
must not recreate Maddy or attempt to rebind SMTP/IMAP ports. Native transport
maintenance remains a separate operation. Post-deploy checks require `web` and
probe the API's configured IMAP and SMTP upstreams with its existing TLS trust,
without credentials or sending mail. This supports an already-running native
transport outside this Compose project. A stopped local Maddy service is
reported explicitly; unreachable/untrusted upstreams still fail deployment.
Protocol reachability is not mailbox-login or delivery verification.
Session cookies stay Secure/HttpOnly/SameSite=Lax and mailbox passwords stay
server-side. The static path never serves Python/secrets or swallows API routes.

| Method | Route | Result |
| --- | --- | --- |
| GET | `/api/mail/session` | `{domain,email,csrf,oauthConfigured,mailboxes,authErrors}`; choices come only from authorized server session |
| GET | `/api/mail/messages?folder=inbox&page=1` | `{messages,more}`, 20 messages/page, inbox/sent only |
| GET | `/api/mail/messages/UID?folder=inbox` | Summary plus plain text `body` and attachment filenames; 10 MiB limit |
| POST | `/api/mail/compose` | Form `{csrf,to,subject,body}`; `{accepted:true,saved:boolean}` |
| POST | `/api/mail/logout` | Form `{csrf}`; `{signedOut:true}` |

Authenticated API failures return JSON, not HTML redirects. CSRF is session-bound;
supplied cross-site Origin is rejected. Compose retains one recipient,
200-character/no-newline subjects, 100000-character bodies and five attempts
per minute per session. An SMTP transport failure is **uncertain**, not safe to
retry automatically. SMTP acceptance is not delivery. `saved:false` means SMTP
accepted but Sent-copy saving failed: **do not resend**.

Run `python -m unittest discover -s MailBox/tests -p test_api.py`.
Deterministic IMAP/SMTP tests do not establish live receipt or OAuth integration.
Before promotion, stage real login, mailbox selection, list/read, compose,
Sent-copy partial failure and logout. Preserve current volumes/secrets; rollback
the previous frontend/backend image pair, not mail data.
