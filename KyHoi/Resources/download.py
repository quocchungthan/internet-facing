"""Download public reference snapshots and pinned game sources; no credentials."""
from pathlib import Path
import urllib.request, json, hashlib, datetime, concurrent.futures

root = Path(__file__).resolve().parents[1]
app = root / 'Projects/TikTok-Live-Turnbased/app'
q = 'f9019ac2303d4b80ef0b82fd0515bfb55a80a62b'
b = 'c36e1046c424e7abf1405904006043f6426b8ee8'
items = []
def add(url, path, purpose, version='snapshot'):
    items.append((url, path, purpose, version))
for name, repo, commit in [('xiangqi', 'xiangqi.js', q), ('board', 'xiangqiboardjs', b)]:
    for f in ['README.md', 'package.json', 'LICENSE' if name == 'xiangqi' else 'LICENSE.md']:
        add(f'https://raw.githubusercontent.com/lengyanyu258/{repo}/{commit}/{f}', f'Resources/Game/{name}-{f}', 'Game API / license', commit)
add(f'https://raw.githubusercontent.com/lengyanyu258/xiangqi.js/{q}/xiangqi.js', 'Projects/TikTok-Live-Turnbased/app/vendor/xiangqi.cjs', 'Pinned rule runtime', q)
for f in ['xiangqiboard.js', 'xiangqiboard.css']:
    add(f'https://raw.githubusercontent.com/lengyanyu258/xiangqiboardjs/{b}/src/{f}', f'Projects/TikTok-Live-Turnbased/app/web/public/vendor/{f}', 'Pinned board renderer', b)
for url, path, purpose in [
 ('https://lengyanyu258.github.io/xiangqiboardjs/docs', 'Game/board-api.html', 'Board API reference'),
 ('https://lengyanyu258.github.io/xiangqiboardjs/examples', 'Game/board-examples.html', 'Board examples'),
 ('https://raw.githubusercontent.com/zerodytrash/TikTok-Live-Connector/master/README.md', 'TikTok/connector-README.md', 'Connector integration'),
 ('https://raw.githubusercontent.com/zerodytrash/TikTok-Live-Connector/master/LICENSE', 'TikTok/connector-LICENSE', 'Connector license'),
 ('https://www.eulerstream.com/docs/quickstart', 'TikTok/euler-quickstart.html', 'Signing key and connection'),
 ('https://www.eulerstream.com/docs/libraries/nodejs', 'TikTok/euler-nodejs.html', 'Node integration'),
 ('https://www.eulerstream.com/pricing', 'TikTok/euler-pricing.html', 'Prices; verify before buying'),
 ('https://www.eulerstream.com/docs', 'TikTok/euler-index.html', 'Documentation entrypoint'),
 ('https://www.eulerstream.com/docs/api/rate-limits', 'TikTok/euler-rate-limits.html', 'Signing quotas and 429 handling'),
 ('https://www.eulerstream.com/docs/api/tiktok-live', 'TikTok/euler-api.html', 'API reference entrypoint'),
 ('https://support.tiktok.com/en/live-gifts-wallet/tiktok-live/what-is-tiktok-live', 'TikTok/live-eligibility.html', 'LIVE account prerequisites'),
 ('https://obsproject.com/kb/browser-source', 'Broadcasting/obs-browser-source.html', 'Capture web UI'),
 ('https://obsproject.com/kb/quick-start-guide', 'Broadcasting/obs-quickstart.html', 'Broadcast setup'),
 ('https://fastify.dev/docs/latest/Reference/Server/', 'Infrastructure/fastify-server.html', 'Server reference'),
 ('https://github.com/fastify/fastify-static/raw/refs/heads/main/README.md', 'Infrastructure/fastify-static.md', 'Static hosting'),
 ('https://vite.dev/guide/', 'Infrastructure/vite-guide.html', 'Frontend build'),
 ('https://nodejs.org/api/sqlite.html', 'Infrastructure/node-sqlite.html', 'SQLite transactions'),
 ('https://raw.githubusercontent.com/websockets/ws/master/README.md', 'Infrastructure/ws-README.md', 'WebSocket server'),
 ('https://caddyserver.com/docs/quick-starts/reverse-proxy', 'Infrastructure/caddy-proxy.html', 'HTTPS deployment'),
 ('https://huggingface.co/convaiinnovations/laya/raw/main/README.md', 'AI/laya-model-card.md', 'Future only; no model weights'),
]: add(url, 'Resources/'+path, purpose)

def fetch(item):
    url, path, purpose, version = item
    record = dict(url=url, path=path, purpose=purpose, version=version, fetchedAt=datetime.datetime.now(datetime.timezone.utc).isoformat())
    try:
        request=urllib.request.Request(url, headers={'User-Agent':'Mozilla/5.0 (ResourceArchive)'})
        with urllib.request.urlopen(request, timeout=45) as response: data=response.read()
        target=root/path; target.parent.mkdir(parents=True, exist_ok=True); target.write_bytes(data)
        record.update(status='downloaded', bytes=len(data), sha256=hashlib.sha256(data).hexdigest())
    except Exception as e: record.update(status='missing', error=str(e))
    return record

if __name__ == '__main__':
    with concurrent.futures.ThreadPoolExecutor(max_workers=6) as pool: results=list(pool.map(fetch, items))
    # Preserve API docs from the actually installed npm packages, not only main-branch docs.
    for pkg, filename, dest in [
        ('tiktok-live-connector','README.md','connector-2.5.0-README.md'),
        ('tiktok-live-connector','LICENSE','connector-2.5.0-LICENSE'),
        ('tiktok-live-connector','dist/index-DcaLUrMQ.d.ts','connector-2.5.0-api.d.ts'),
        ('tiktok-live-proto','LICENSE','proto-0.2.4-LICENSE')]:
        base=app/'node_modules'/pkg
        if not (base/filename).exists(): continue
        meta=json.loads((base/'package.json').read_text(encoding='utf-8'))
        data=(base/filename).read_bytes(); target=root/'Resources/TikTok'/dest; target.write_bytes(data)
        results.append(dict(url=f'https://registry.npmjs.org/{pkg}/-/{pkg}-{meta["version"]}.tgz',
            path=target.relative_to(root).as_posix(),purpose='Installed package reference / license',version=meta['version'],
            archiveMember=filename,fetchedAt=datetime.datetime.now(datetime.timezone.utc).isoformat(),status='downloaded',bytes=len(data),sha256=hashlib.sha256(data).hexdigest()))
    (root/'Resources/source-index.json').write_text(json.dumps(results, ensure_ascii=False, indent=2), encoding='utf-8')
    lines=['# Resource snapshots', '', 'Public sources downloaded for this MVP. HTML is an archived page, not an offline website. Login-only dashboards and account-specific RTMP credentials are not downloaded.', '', '| Status | Local file | Version | Purpose | Source |', '|---|---|---|---|---|']
    for r in results:
        lines.append(f"| {r['status']} | [{r['path']}](../{r['path']}) | {r['version']} | {r['purpose']} | [source]({r['url']}) |")
    lines += ['', 'See source-index.json for UTC retrieval time, SHA-256 and errors. Re-running this script refreshes unpinned reference snapshots. Runtime game sources remain pinned.']
    (root/'Resources/source-index.md').write_text('\n'.join(lines)+'\n', encoding='utf-8')
    print(json.dumps([{'path':r['path'],'status':r['status'],'error':r.get('error')} for r in results], indent=2))
