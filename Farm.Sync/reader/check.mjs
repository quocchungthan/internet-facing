// Read-only setup check for the AFFiNE side of the sync. Prints PASS/FAIL lines with a plain-language fix.
// Never prints secrets. Page titles and IDs are only printed with --show-ids (use it in a private terminal, not in CI logs).
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import pg from 'pg';
import { decodeDocument } from './decode.mjs';

const showIds = process.argv.includes('--show-ids');
let failures = 0;
const pass = label => console.log(`PASS  ${label}`);
const info = label => console.log(`INFO  ${label}`);
function fail(label, fix) {
  failures++;
  console.log(`FAIL  ${label}`);
  if (fix) console.log(`      -> ${fix}`);
}

const REQUIRED = {
  AFFINE_DATABASE_URL: 'the read-only AFFiNE database URL (see reader/provision-source-role.sql)',
  AFFINE_WORKSPACE_ID: 'the id of the AFFiNE workspace to publish from',
  AFFINE_COLLECTION_ID: 'the id of the collection that holds the Meta and Feeds pages',
  AFFINE_META_DOC_ID: 'the id of the Meta page',
  AFFINE_FEEDS_DOC_ID: 'the id of the Feeds page',
  AFFINE_FEEDS_BLOCK_ID: 'the id of the block in the Feeds page',
};
const OPTIONAL = ['AFFINE_PORTFOLIO_COLLECTION_ID', 'AFFINE_GIGS_COLLECTION_ID'];

const env = name => process.env[name]?.trim() ?? '';
const missing = Object.keys(REQUIRED).filter(name => !env(name));
for (const name of Object.keys(REQUIRED).filter(name => env(name))) pass(`${name} is set`);
for (const name of missing) fail(`${name} is not set`, `add it to sync.env: ${REQUIRED[name]}.`);
for (const name of OPTIONAL) {
  if (env(name)) pass(`${name} is set`);
  else info(`${name} is not set, so that part of the portfolio will not be synced`);
}
if (missing.length) finish();

const client = new pg.Client({ connectionString: env('AFFINE_DATABASE_URL'), connectionTimeoutMillis: 10000, statement_timeout: 30000 });
try {
  await client.connect();
  pass('Logged in to the AFFiNE database');
} catch (error) {
  fail(`Could not log in to the AFFiNE database (${error.code ?? error.message})`,
    'check the host (the AFFiNE compose service name, usually "postgres"), the database name (usually "affine"), and that the affine_sync role exists with the password in AFFINE_DATABASE_URL. Create the role with reader/provision-source-role.sql.');
  finish();
}

try {
  const workspaceId = env('AFFINE_WORKSPACE_ID');
  const count = async text => Number((await client.query(text, [workspaceId])).rows[0].n);
  let snapshots;
  try {
    snapshots = await count('SELECT count(*) AS n FROM snapshots WHERE workspace_id = $1');
    pass('The read-only role can read the AFFiNE tables');
  } catch (error) {
    fail(`The read-only role cannot read the AFFiNE tables (${error.code ?? error.message})`,
      'run reader/provision-source-role.sql as the AFFiNE database owner.');
    finish();
  }
  if (snapshots === 0) {
    fail('No data found for AFFINE_WORKSPACE_ID',
      'either production AFFiNE is empty (restore your workspace with scripts/affine-collections.sh restore) or the id is wrong.');
    if (showIds) {
      const found = (await client.query('SELECT DISTINCT workspace_id FROM snapshots LIMIT 10')).rows.map(row => row.workspace_id);
      info(`workspaces in this AFFiNE: ${found.join(', ') || '(none)'}`);
    }
    finish();
  }
  pass('The workspace exists and has data');

  const root = decodeDocument((await client.query(
    'SELECT blob, updated_at AS timestamp FROM snapshots WHERE workspace_id = $1 AND guid = $1 UNION ALL SELECT blob, created_at FROM updates WHERE workspace_id = $1 AND guid = $1 ORDER BY timestamp',
    [workspaceId])).rows);
  const collections = root.setting?.collections ?? [];
  const pages = root.meta?.pages ?? [];
  pass(`The workspace has ${pages.filter(page => !page.trash).length} pages and ${collections.length} collections`);

  const required = [
    ['Meta page', env('AFFINE_META_DOC_ID'), 'AFFINE_META_DOC_ID must be the id of the Meta page.'],
    ['Feeds page', env('AFFINE_FEEDS_DOC_ID'), 'AFFINE_FEEDS_DOC_ID must be the id of the Feeds page.'],
    ['page-properties document', `db$${workspaceId}$docProperties`, 'AFFiNE creates it when a page gets a property (such as Kind); set a property on any page in the AFFiNE UI.'],
    ['custom-property definitions', `db$${workspaceId}$docCustomPropertyInfo`, 'AFFiNE creates it when you add a custom property (such as Kind) in the AFFiNE UI.'],
  ];
  for (const [label, guid, fix] of required) {
    const has = Number((await client.query(
      'SELECT (SELECT count(*) FROM snapshots WHERE workspace_id = $1 AND guid = $2) + (SELECT count(*) FROM updates WHERE workspace_id = $1 AND guid = $2) AS n',
      [workspaceId, guid])).rows[0].n);
    if (has) pass(`The ${label} has stored data`);
    else fail(`The ${label} has no stored data in the database`, fix);
  }

  const titleOf = id => pages.find(page => page.id === id)?.title ?? '(unknown)';
  const collection = collections.find(c => c.id === env('AFFINE_COLLECTION_ID'));
  if (!collection) {
    fail('AFFINE_COLLECTION_ID is not a collection of this workspace',
      'open the collection in AFFiNE and copy its id from the address bar, or compare with --show-ids.');
    if (showIds) info(`collections: ${collections.map(c => `${c.name} (${c.id})`).join(', ')}`);
  } else {
    pass('The Meta/Feeds collection exists');
    for (const [name, label] of [['AFFINE_META_DOC_ID', 'Meta'], ['AFFINE_FEEDS_DOC_ID', 'Feeds']]) {
      const id = env(name);
      if (!pages.some(page => page.id === id && !page.trash)) {
        fail(`${label} page is missing or in the trash`, `${name} must be the id of an existing page; restore it in AFFiNE or fix the id.`);
      } else if (!collection.allowList?.includes(id)) {
        fail(`${label} page is not in the collection`, `add the ${label} page to that collection in AFFiNE (the sync only trusts pages that are explicit members).`);
      } else pass(`${label} page exists and is in the collection`);
    }
    if (showIds) info(`pages in the collection: ${(collection.allowList ?? []).map(id => `${titleOf(id)} (${id})`).join(', ')}`);
  }

  for (const name of OPTIONAL.filter(name => env(name))) {
    const found = collections.find(c => c.id === env(name));
    if (!found) {
      fail(`${name} is not a collection of this workspace`, 'copy the collection id again, or unset it to skip that part.');
      if (showIds) info(`collections: ${collections.map(c => `${c.name} (${c.id})`).join(', ')}`);
    } else {
      pass(`${name}: collection found with ${(found.allowList ?? []).length} pages`);
    }
  }
} catch (error) {
  fail(`Unexpected problem reading AFFiNE data (${error.message})`);
} finally {
  await client.end();
}

if (!failures) {
  // Run the real exporter so the report shows exactly what the sync would publish.
  const run = spawnSync('node', [fileURLToPath(new URL('./export.mjs', import.meta.url))], {
    encoding: 'utf8', maxBuffer: 256 * 1024 * 1024, env: process.env,
  });
  const skipped = (run.stderr ?? '').split('\n').filter(line => line.startsWith('Skipped '));
  if (run.status !== 0) {
    fail('The exporter failed', showIds ? (run.stderr ?? '').split('\n').find(line => line.trim()) : 'run the check with --show-ids in a private terminal to see the reason.');
  } else {
    try {
      const documents = JSON.parse(run.stdout).documents;
      const rows = key => documents.reduce((total, document) => total + (document[key]?.length ?? 0), 0);
      pass(`Export works: ${documents.length} documents (profiles ${rows('profiles')}, projects ${rows('projects')}, experiences ${rows('experiences')}, skills ${rows('skills')}, social links ${rows('socialLinks')}, gigs ${rows('gigs')}, metadata ${rows('metadata')}, bookmarks ${rows('bookmarks')})`);
    } catch {
      fail('The exporter returned unreadable output');
    }
    if (skipped.length) {
      fail(`${skipped.length} page(s) would be skipped because their content is invalid`,
        'fix the page properties in AFFiNE; the sync keeps the last good version meanwhile. Run with --show-ids to see which pages.');
      if (showIds) skipped.forEach(line => info(line));
    }
  }
}
finish();

function finish() {
  process.exit(failures ? 1 : 0);
}
