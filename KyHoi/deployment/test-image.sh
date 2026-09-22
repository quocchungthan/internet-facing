#!/usr/bin/env bash
set -Eeuo pipefail
image=${1:?Pass image tag}
name=kyhoi-smoke-$$
volume=$name-data
cleanup() { docker rm -f "$name" >/dev/null 2>&1 || true; docker volume rm "$volume" >/dev/null 2>&1 || true; }
trap cleanup EXIT
docker volume create "$volume" >/dev/null
docker run --rm --network none --user 0 --entrypoint sh -v "$volume:/data" "$image" -c 'chown 1000:1000 /data'
docker run -d --name "$name" --network host --read-only --tmpfs /tmp --cap-drop ALL --security-opt no-new-privileges -e PORT=13400 -v "$volume:/data" "$image" >/dev/null
wait_health() {
  for ((i=0;i<30;i++)); do
    if [[ $(docker inspect -f '{{.State.Health.Status}}' "$name") == healthy ]]; then return; fi
    sleep 2
  done
  docker logs "$name"; return 1
}
wait_health
code=$(docker exec -i "$name" node --input-type=module <<'JS'
import assert from 'node:assert/strict';
const base='http://127.0.0.1:13400';
async function api(path,ip,body) {
  const r=await fetch(base+path,{method:body?'POST':'GET',headers:{'content-type':'application/json','x-forwarded-for':ip,origin:'https://kyhoi.shuneo.com',host:'kyhoi.shuneo.com','x-forwarded-proto':'https'},body:body?JSON.stringify(body):undefined});
  assert.ok(r.ok,await r.clone().text());return r.json();
}
const room=await api('/api/rooms','192.0.2.1',{name:'Host',settings:{turnSeconds:300}});
const member=await api(`/api/rooms/${room.code}/join`,'192.0.2.2',{name:'Player'});
assert.notEqual(room.me.id,member.me.id);assert.equal(member.isHost,false);
await api(`/api/rooms/${room.code}/team`,'192.0.2.2',{team:'red'});
console.log(room.code);
JS
)
docker restart "$name" >/dev/null
wait_health
docker exec -e ROOM_CODE="$code" -i "$name" node --input-type=module <<'JS'
import assert from 'node:assert/strict';
const r=await fetch(`http://127.0.0.1:13400/api/rooms/${process.env.ROOM_CODE}`,{headers:{'x-forwarded-for':'192.0.2.2'}});
assert.equal(r.status,200);const room=await r.json();assert.equal(room.me.team,'red');assert.equal(room.me.name,'Player');assert.equal(room.isHost,false);
console.log('Image smoke passed: non-root read-only runtime, trusted proxy identity, room/team persistence across restart.');
JS
