// Restructures the self-hosted AFFiNE test workspace so that its pages match the
// Farm.Data schema (page = record, page properties = scalar columns, page body = prose).
// It edits AFFiNE's own database, so back it up first (scripts/affine-collections.sh backup)
// and stop the AFFiNE server while it runs:  docker stop affine_server
//
//   AFFINE_ADMIN_DATABASE_URL=postgresql://affine@<postgres-ip>:5432/affine node seed-structure.mjs [--apply]
//
// Without --apply it only reports what it would change. Existing pages are never rewritten:
// re-running skips pages whose deterministic id already exists.
import { createHash, randomBytes } from 'node:crypto';
import pg from 'pg';
import * as Y from 'yjs';

const W = process.env.AFFINE_WORKSPACE_ID ?? 'da7da136-e099-4224-b64d-4fccd115eb89';
const USER = process.env.AFFINE_USER_ID ?? '995400ff-5b06-4469-95b3-64a0dc11ea54';
const URL = process.env.AFFINE_ADMIN_DATABASE_URL;
if (!URL) throw new Error('AFFINE_ADMIN_DATABASE_URL is required');
const APPLY = process.argv.includes('--apply');
const PORTFOLIO_COLLECTION = 'e9yeGpEb0hV3KCsdd-wWM';
const GIGS_COLLECTION = '4XdLJej6V8iFFZ6BuPAQk';
const FEEDS = 'e0cfaCrD_RsruYoDSwFgE';
const FEEDS_BLOCK = 'FPh-Fksvis';

const id = key => createHash('sha256').update(`affine-seed:${key}`).digest('base64url').slice(0, 21);
const rnd = () => randomBytes(9).toString('base64url');
const now = Date.now();

const client = new pg.Client({ connectionString: URL });
await client.connect();
const docs = new Map(); // guid -> { doc, isNew }
async function load(guid) {
  if (docs.has(guid)) return docs.get(guid).doc;
  const { rows } = await client.query('SELECT blob FROM snapshots WHERE workspace_id=$1 AND guid=$2', [W, guid]);
  const doc = new Y.Doc({ guid });
  rows.forEach(r => Y.applyUpdate(doc, r.blob));
  docs.set(guid, { doc, isNew: rows.length === 0, changed: false });
  return doc;
}
const touch = guid => { docs.get(guid).changed = true; };

// ---- block helpers -------------------------------------------------------
function block(doc, flavour, version, props = {}, children = [], text) {
  const blockId = rnd();
  const m = new Y.Map();
  doc.getMap('blocks').set(blockId, m);
  m.set('sys:id', blockId);
  m.set('sys:flavour', flavour);
  m.set('sys:version', version);
  m.set('sys:children', Y.Array.from(children));
  if (text !== undefined) m.set('prop:text', new Y.Text(text));
  for (const [k, v] of Object.entries(props)) m.set(k, v);
  return blockId;
}
const paragraph = (doc, text, type = 'text', children = []) =>
  block(doc, 'affine:paragraph', 1, { 'prop:type': type, 'prop:collapsed': false }, children, text);
const listItem = (doc, text, type, children = []) =>
  block(doc, 'affine:list', 1, { 'prop:type': type, 'prop:checked': false, 'prop:collapsed': false }, children, text);

function createPage(guid, title, bodyBuilder) {
  const doc = new Y.Doc({ guid });
  docs.set(guid, { doc, isNew: true, changed: true });
  const surface = block(doc, 'affine:surface', 5, {});
  const surfaceMap = doc.getMap('blocks').get(surface);
  const elements = new Y.Map();
  elements.set('type', '$blocksuite:internal:native$');
  elements.set('value', new Y.Map());
  surfaceMap.set('prop:elements', elements);
  const bg = new Y.Map(); bg.set('dark', '#252525'); bg.set('light', '#ffffff');
  const edgeless = new Y.Map();
  const style = new Y.Map();
  style.set('borderRadius', 8); style.set('borderSize', 4); style.set('borderStyle', 'none');
  style.set('shadowType', '--affine-note-shadow-box');
  edgeless.set('style', style);
  const children = bodyBuilder(doc);
  const note = block(doc, 'affine:note', 1, {
    'prop:xywh': '[0,0,498,92]', 'prop:background': bg, 'prop:index': 'a0', 'prop:lockedBySelf': false,
    'prop:hidden': false, 'prop:displayMode': 'both', 'prop:edgeless': edgeless,
  }, children);
  const page = block(doc, 'affine:page', 2, { 'prop:title': new Y.Text(title) }, [surface, note]);
  void page;
  return doc;
}

function database(doc, title, columns, rows) {
  // columns[0] is the title column; each row is an array of values in column order.
  const colDefs = columns.map((c, i) => ({
    id: i === 0 ? 'title' : rnd(), name: c.name, type: i === 0 ? 'title' : c.type, data: {},
  }));
  const rowIds = rows.map(r => paragraph(doc, String(r[0])));
  const dbId = block(doc, 'affine:database', 3, {}, rowIds);
  const db = doc.getMap('blocks').get(dbId);
  db.set('prop:title', new Y.Text(title));
  const cols = new Y.Array();
  colDefs.forEach(c => {
    const m = new Y.Map();
    ['type', 'name', 'id'].forEach(k => m.set(k, c[k]));
    m.set('data', new Y.Map());
    cols.push([m]);
  });
  db.set('prop:columns', cols);
  const cells = new Y.Map();
  rows.forEach((row, r) => {
    const rowCells = new Y.Map();
    columns.forEach((c, i) => {
      if (i === 0) return;
      const cell = new Y.Map();
      cell.set('columnId', colDefs[i].id);
      cell.set('value', c.type === 'rich-text' ? new Y.Text(String(row[i])) : row[i]);
      rowCells.set(colDefs[i].id, cell);
    });
    cells.set(rowIds[r], rowCells);
  });
  db.set('prop:cells', cells);
  const view = new Y.Map();
  view.set('mode', 'table'); view.set('id', rnd()); view.set('name', 'Table View');
  const viewCols = new Y.Array();
  colDefs.forEach(c => { const m = new Y.Map(); m.set('id', c.id); m.set('hide', false); m.set('width', c.id === 'title' ? 260 : 180); viewCols.push([m]); });
  view.set('columns', viewCols);
  view.set('filter', { type: 'group', op: 'and', conditions: [] });
  view.set('header', { titleColumn: 'title', iconColumn: 'type' });
  const views = new Y.Array(); views.push([view]);
  db.set('prop:views', views);
  return dbId;
}

// ---- workspace-level docs ------------------------------------------------
const root = await load(W);
const props = await load(`db$${W}$docProperties`);
const defs = await load(`db$${W}$docCustomPropertyInfo`);
const defIds = new Map(); // property name -> definition id (active only)
for (const key of defs.share.keys()) {
  const m = defs.getMap(key);
  if (m.get('name') && !m.get('isDeleted')) defIds.set(m.get('name'), key);
}
let order = 60;
function ensureDefinition(name, type) {
  if (defIds.has(name)) return defIds.get(name);
  const defId = rnd();
  const m = defs.getMap(defId);
  m.set('name', name); m.set('type', type);
  m.set('index', `a${order++}0${rnd().slice(0, 30)}`);
  m.set('isDeleted', false); m.set('id', defId);
  defIds.set(name, defId); touch(`db$${W}$docCustomPropertyInfo`);
  return defId;
}
const TYPES = { IsFeatured: 'checkbox', IsCurrent: 'checkbox', DisplayOrder: 'number' };
function setProperties(guid, values) {
  const m = props.getMap(guid);
  m.set('id', guid); m.set('primaryMode', 'page'); m.set('edgelessColorTheme', 'dark');
  m.set('createdBy', USER); m.set('updatedBy', USER);
  for (const [name, value] of Object.entries(values))
    m.set(`custom:${ensureDefinition(name, TYPES[name] ?? 'text')}`, value);
  touch(`db$${W}$docProperties`);
}

const metaPages = root.getMap('meta').get('pages');
const spaces = root.getMap('spaces');
const collections = root.getMap('setting').get('collections');
function register(guid, title, collectionId) {
  const m = new Y.Map();
  m.set('id', guid); m.set('title', title); m.set('createDate', now); m.set('tags', new Y.Array());
  metaPages.push([m]);
  spaces.set(guid, new Y.Doc({ guid }));
  const index = collections.toArray().findIndex(c => c.id === collectionId);
  const collection = collections.get(index);
  collections.delete(index, 1);
  collections.insert(index, [{ ...collection, allowList: [...collection.allowList, guid] }]);
  touch(W);
}

const created = [];
function addPage(key, title, collectionId, kind, values, bodyBuilder) {
  const guid = id(key);
  if (metaPages.toArray().some(p => p.get('id') === guid)) return;
  createPage(guid, title, bodyBuilder);
  register(guid, title, collectionId);
  setProperties(guid, { Kind: kind, ...values });
  created.push(`${kind}: ${title}`);
}
const text = lines => doc => lines.map(l => paragraph(doc, l));

// ---- Portfolio collection (KaiPortfolio) ---------------------------------
addPage('profile', 'Profile', PORTFOLIO_COLLECTION, 'Profile', {
  DisplayName: 'quocchungthan', Headline: 'An elder engineer learning how to vibe code',
  ContactEmail: 'chung@eldervibe.dev',
}, text(["If you are seeking for a software developer to talk to, it's me here."]));

addPage('project-internet-facing', 'internet-facing', PORTFOLIO_COLLECTION, 'Project', {
  Slug: 'internet-facing', Summary: 'Personal website and the AuraFarming data layer (Farm.Data).',
  SourceUrl: 'https://github.com/quocchungthan/internet-facing', Skills: 'C#, .NET, EF Core, PostgreSQL',
  IsFeatured: true, DisplayOrder: 1,
}, text([
  'The website plus Farm.Data, the Entity Framework Core package that owns the AuraFarming PostgreSQL schema.',
  'Farm.Data holds website metadata, platform bookmarks, the portfolio and the solved gig problems, and is published to GitHub Packages.',
]));

addPage('project-affine-sync', 'AFFiNE to AuraFarming sync', PORTFOLIO_COLLECTION, 'Project', {
  Slug: 'affine-github-repo', Summary: 'Worker that mirrors a self-hosted AFFiNE workspace into PostgreSQL.',
  SourceUrl: 'https://github.com/quocchungthan/affine-github-repo', Skills: 'C#, .NET, EF Core, PostgreSQL, Node.js, Yjs, Docker',
  IsFeatured: true, DisplayOrder: 2,
}, text([
  'A .NET worker that calls a Node reader, decodes the AFFiNE Yjs documents straight from its PostgreSQL database, and upserts them through Farm.Data.',
  'AFFiNE stays the source of truth; the seed data in Farm.Data only provides sample rows.',
]));

const skillRows = [
  ['C#', 'Language', 1], ['.NET', 'Framework', 2], ['EF Core', 'Framework', 3], ['PostgreSQL', 'Database', 4],
  ['Node.js', 'Runtime', 5], ['Yjs', 'Library', 6], ['Docker', 'Tooling', 7],
];
addPage('skills', 'Skills', PORTFOLIO_COLLECTION, 'Skills', {}, doc =>
  [database(doc, 'Skills.Data', [{ name: 'Name' }, { name: 'Category', type: 'rich-text' }, { name: 'DisplayOrder', type: 'number' }], skillRows)]);

const socialRows = [
  ['GitHub', 'https://github.com/quocchungthan', 1],
  ['TikTok', 'https://www.tiktok.com/@eldervibe.dev', 2],
  ['Facebook', 'https://www.facebook.com/eldervibe.dev', 3],
];
addPage('social-links', 'Social Links', PORTFOLIO_COLLECTION, 'SocialLinks', {}, doc =>
  [database(doc, 'SocialLinks.Data', [{ name: 'Label' }, { name: 'Url', type: 'link' }, { name: 'DisplayOrder', type: 'number' }], socialRows)]);

// Work history modelled on the public portfolio https://kvi-dev.vercel.app (education omitted).
const bullets = lines => doc => lines.map(l => listItem(doc, l, 'bulleted'));
addPage('experience-mitek', 'Senior Software Engineer', PORTFOLIO_COLLECTION, 'Experience', {
  Company: 'MiTek', EmploymentType: 'FullTime', WorkMode: 'Hybrid',
  StartDate: '2025-09-01', IsCurrent: true, DisplayOrder: 1,
}, bullets([
  'Core production systems for North American structural fabrication workflows.',
  'Redesigned geometry and conversion algorithms to improve output accuracy and reduce recurring production defects.',
  'Led cross-system root cause analysis and improved architecture, maintainability and internal debugging tools.',
  'Drove technical decisions and raised engineering standards through architecture reviews, mentoring and knowledge sharing.',
]));
addPage('experience-ptn', 'Software Engineer', PORTFOLIO_COLLECTION, 'Experience', {
  Company: 'PTN Global Corp', EmploymentType: 'FullTime', WorkMode: 'Hybrid',
  StartDate: '2022-08-01', EndDate: '2025-09-01', IsCurrent: false, DisplayOrder: 2,
}, bullets([
  'Full-stack development of real-time language products for international clients.',
  'Designed backend services and the data model for a live caption translation platform.',
  'Redesigned the delivery workflow with the team lead, cutting time to ship features by 40%.',
  'Built a real-time transcription pipeline for a meeting bot across Zoom, Teams, Meet and Webex.',
]));

// ---- Gigs collection -----------------------------------------------------
addPage('gig-affine-sync', 'Syncing AFFiNE collections into PostgreSQL', GIGS_COLLECTION, 'Gig', {
  Slug: 'syncing-affine-into-postgres', Category: 'Data integration',
  Summary: 'Keep a self-hosted AFFiNE workspace as the source of truth and mirror its collections into the AuraFarming database.',
  Skills: 'Yjs, PostgreSQL, Docker, EF Core', DisplayOrder: 1,
}, doc => {
  const step = (title, description) => listItem(doc, title, 'numbered', [paragraph(doc, description)]);
  return [
    paragraph(doc, 'Problem', 'h2'),
    paragraph(doc, 'The sync worker failed with "Missing/invalid Feeds.Platform". AFFiNE had stored the row titles as page links and the long text lived in page bodies, neither of which the reader understood.'),
    paragraph(doc, 'Steps', 'h2'),
    step('Reproduce against the live data', 'Read the Yjs snapshots read-only from the AFFiNE database and decode them to see where each value really lives.'),
    step('Compare AFFiNE with the Farm.Data schema', 'List every table, column and length limit in the EF model and match it to a page, a page property or a page body.'),
    step('Extend Farm.Data', 'Add the gig tables and a SourceDocId column so each page can be upserted by its AFFiNE page id.'),
    step('Restructure the AFFiNE pages', 'One page per record: properties hold the short values, the page body holds the description. A script backs up and restores the workspace documents.'),
    paragraph(doc, 'Outcome', 'h2'),
    paragraph(doc, 'In progress: the reader is being extended to map page properties and page bodies.'),
  ];
});

// ---- Feeds: plain-text titles and an IsActive column ---------------------
const feeds = await load(FEEDS);
const feedsDb = feeds.getMap('blocks').get(FEEDS_BLOCK);
const names = { wFuAwzzoUD: 'GitHub', PSuE4UqINu: 'TikTok', Sk0GXEmJxb: 'Facebook' };
for (const [rowId, name] of Object.entries(names)) {
  const t = feeds.getMap('blocks').get(rowId).get('prop:text');
  if (t.toString() !== name) { t.delete(0, t.length); t.insert(0, name); touch(FEEDS); created.push(`Feeds title: ${name}`); }
}
const columns = feedsDb.get('prop:columns');
if (!columns.toArray().some(c => c.get('name') === 'IsActive')) {
  const colId = rnd();
  const m = new Y.Map();
  m.set('type', 'checkbox'); m.set('name', 'IsActive'); m.set('data', new Y.Map()); m.set('id', colId);
  columns.push([m]);
  const viewColumns = feedsDb.get('prop:views').get(0).get('columns');
  const vc = new Y.Map(); vc.set('id', colId); vc.set('hide', false); vc.set('width', 120); viewColumns.push([vc]);
  for (const rowId of feedsDb.get('sys:children').toArray()) {
    const cell = new Y.Map(); cell.set('columnId', colId); cell.set('value', true);
    feedsDb.get('prop:cells').get(rowId).set(colId, cell);
  }
  touch(FEEDS); created.push('Feeds: IsActive column');
}

// ---- Meta: SpecifiedIssueUrl is a GitHub issue link ----------------------
const META_DOC = 'H5y_LGDY5d0uP9TLHN2BS';
const ISSUE_URL = 'https://github.com/quocchungthan/internet-facing/issues/32';
const issueKey = `custom:${ensureDefinition('SpecifiedIssueUrl', 'text')}`;
if (props.getMap(META_DOC).get(issueKey) !== ISSUE_URL) {
  props.getMap(META_DOC).set(issueKey, ISSUE_URL); touch(`db$${W}$docProperties`); created.push('Meta: SpecifiedIssueUrl');
}

// ---- Gig outcome: the sync now works --------------------------------------
const GIG = id('gig-affine-sync');
const OUTCOME = 'Solved: the worker now syncs the Profile, Projects, Skills, Social Links and Gigs pages (properties and page bodies) into PostgreSQL through Farm.Data, and the website serves them from the same database. The seed data stays only as sample rows.';
if (metaPages.toArray().some(p => p.get('id') === GIG)) {
  const gig = await load(GIG);
  const blocks = gig.getMap('blocks');
  const children = [...blocks.values()].find(b => b.get('sys:flavour') === 'affine:note')?.get('sys:children').toArray() ?? [];
  const at = children.findIndex(c => blocks.get(c).get('prop:text')?.toString() === 'Outcome');
  const target = at >= 0 ? blocks.get(children[at + 1]) : undefined;
  if (target && target.get('prop:text').toString() !== OUTCOME) {
    const t = target.get('prop:text'); t.delete(0, t.length); t.insert(0, OUTCOME);
    touch(GIG); created.push('Gig: Outcome updated');
  }
}

// ---- write ---------------------------------------------------------------
console.log(created.length ? created.map(c => `+ ${c}`).join('\n') : 'Nothing to change.');
if (!APPLY) { console.log('\nDry run. Re-run with --apply to write.'); await client.end(); process.exit(0); }
await client.query('BEGIN');
for (const [guid, entry] of docs) {
  if (!entry.changed) continue;
  const blob = Buffer.from(Y.encodeStateAsUpdate(entry.doc));
  const state = Buffer.from(Y.encodeStateVector(entry.doc));
  await client.query(`
    INSERT INTO snapshots (guid, workspace_id, blob, state, created_at, updated_at, created_by, updated_by)
    VALUES ($1,$2,$3,$4,now(),now(),$5,$5)
    ON CONFLICT (workspace_id, guid) DO UPDATE SET blob=$3, state=$4, updated_at=now(), updated_by=$5`,
  [guid, W, blob, state, USER]);
  console.log(`wrote ${guid}`);
}
await client.query('COMMIT');
await client.end();
