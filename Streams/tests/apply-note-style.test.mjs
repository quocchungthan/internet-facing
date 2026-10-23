import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { mkdtemp, mkdir, writeFile, readFile, rm } from 'node:fs/promises';
import { homedir, tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { spawnSync } from 'node:child_process';
import test from 'node:test';

const unix = path => process.platform === 'win32'
  ? `/${path[0].toLowerCase()}${path.slice(2).replaceAll('\\', '/')}` : path;
const bash = process.platform === 'win32'
  ? join(homedir(), 'AppData', 'Local', 'Programs', 'Git', 'bin', 'bash.exe') : 'bash';
const base = `ghcr.io/toeverything/affine@sha256:${'a'.repeat(64)}`;
const revision = 'b'.repeat(40);

for (const scenario of ['valid', 'index-digest', 'version-mismatch', 'readiness-failure']) {
  test(`native AFFiNE image-only installer: ${scenario}`, async () => {
    const directory = await mkdtemp(join(tmpdir(), 'note-style-test-'));
    try {
      const deployment = join(directory, 'deployment');
      await mkdir(deployment);
      await writeFile(join(deployment, 'compose.yml'), 'services:\n  affine:\n    image: native\n');
      await writeFile(join(directory, 'affine-image.txt'), `${base}\n`);
      await writeFile(join(directory, 'calls.log'), '');
      const css = '/* verified fixture stylesheet */\n';
      await writeFile(join(directory, 'affine.css'), css);
      const commands = {
        docker: `#!/usr/bin/env bash
set -eu
printf '%s\\n' "$*" >> "$CALL_LOG"
case "$1 $2" in
  "compose -f") if [[ "$*" == *"ps -q affine" ]]; then echo native-container; fi ;;
  "inspect --format") echo sha256:native-image ;;
  "image inspect")
    if [[ "$*" == *RepoDigests* ]]; then
      if [[ "$SCENARIO" == version-mismatch || "$SCENARIO" == index-digest ]]; then echo native-index-digest; else echo "$EXPECTED_BASE"; fi
    elif [[ "$*" == *affine-base* ]]; then
      if [[ "\${!#}" == sha256:native-image ]]; then echo '<no value>'; else echo "$EXPECTED_BASE"; fi
    elif [[ "$*" == *revision* ]]; then echo "$EXPECTED_REVISION"
    elif [[ "$*" == *'.Id'* ]]; then
      if [[ "$SCENARIO" == version-mismatch ]]; then echo sha256:different-image; else echo sha256:native-image; fi
    fi ;;
  "pull $EXPECTED_BASE") ;;
  "load --input") ;;
  *) echo "Unexpected Docker command: $*" >&2; exit 2 ;;
esac
`,
        curl: `#!/usr/bin/env bash
if [[ "$SCENARIO" == readiness-failure ]]; then echo wrong-stylesheet; else cat "$FIXTURE_CSS"; fi
`,
        sleep: 'return 0\n',
        flock: 'return 0\n',
      };
      await writeFile(join(directory, 'commands.sh'), Object.entries(commands)
        .map(([name, content]) => `${name}() {\n${content.replace('#!/usr/bin/env bash\n', '')}\n}\n`).join('\n'));
      const result = spawnSync(bash, [unix(resolve('Streams/ApplyNoteStyle.sh'))], {
        encoding: 'utf8', timeout: 30_000,
        env: { ...process.env, BASH_ENV: unix(join(directory, 'commands.sh')),
          AFFINE_DEPLOY_DIR: unix(deployment), SINGLE_UI_STYLE_DIR: unix(directory),
          SINGLE_UI_REVISION: revision, SINGLE_UI_IMAGE_ARCHIVE: '/fixture-image.tar.gz',
          SINGLE_UI_CSS_SHA: createHash('sha256').update(css).digest('hex'),
          EXPECTED_BASE: base, EXPECTED_REVISION: revision, SCENARIO: scenario,
          FIXTURE_CSS: unix(join(directory, 'affine.css')), CALL_LOG: unix(join(directory, 'calls.log')) },
      });
      assert.ifError(result.error);
      const calls = await readFile(join(directory, 'calls.log'), 'utf8');
      assert.doesNotMatch(calls, /up -d(?:\r?\n|$)|run .*migration|down|volume/);
      if (scenario === 'valid' || scenario === 'index-digest') {
        assert.equal(result.status, 0, result.stderr);
        assert.match(result.stdout, /Verified AFFiNE stylesheet/);
        assert.match(calls, /up -d --no-deps affine/);
        assert.match(await readFile(join(deployment, 'single-ui.compose.yml'), 'utf8'), /affine_migration:/);
        if (scenario === 'index-digest') assert.match(calls, /pull ghcr.io/);
      } else if (scenario === 'version-mismatch') {
        assert.notEqual(result.status, 0);
        assert.match(result.stderr, /version mismatch/);
        assert.doesNotMatch(calls, /load|up -d/);
      } else {
        assert.notEqual(result.status, 0);
        assert.match(result.stderr, /restoring the previous image/);
        assert.equal((calls.match(/up -d --no-deps affine/g) ?? []).length, 2);
        assert.match(await readFile(join(deployment, 'single-ui.compose.yml'), 'utf8'), /sha256:native-image/);
      }
    } finally { await rm(directory, { recursive: true, force: true }); }
  });
}
