import assert from 'node:assert/strict';
import { test } from 'node:test';
import * as Y from 'yjs';
import { decodeDocument, findToolsPage, mapBookmarks, mapMetadata, mapTools, contentHash, skippedPageDiagnostic } from './decode.mjs';

test('merges snapshot and pending CRDT updates', () => {
  const doc = new Y.Doc();
  doc.getMap('blocks').set('a', 'original');
  const snapshot = Y.encodeStateAsUpdate(doc);
  const vector = Y.encodeStateVector(doc);
  doc.getMap('blocks').set('a', 'edited');
  const update = Y.encodeStateAsUpdate(doc, vector);
  assert.equal(decodeDocument([{ blob: snapshot }, { blob: update }]).blocks.a, 'edited');
  doc.destroy();
});

test('maps Meta by active custom property names, not page title', () => {
  const definitions = Object.fromEntries(['DeveloperName', 'Title', 'Description', 'RepositoryUrl']
    .map(name => [name, { name, id: name, isDeleted: false }]));
  const properties = Object.fromEntries(Object.keys(definitions).map(name => [`custom:${name}`, name]));
  const result = mapMetadata(properties, definitions, { createDate: 0 }, 1, '2026-10-05T00:00:00Z');
  assert.equal(result.Title, 'Title');
  assert.equal(result.Id, 1);
  assert.throws(() => mapMetadata({}, definitions, { createDate: 0 }, 1, '2026-10-05T00:00:00Z'));
});

test('maps an optional GitHub issue link for Meta.SpecifiedIssueUrl', () => {
  const names = ['DeveloperName', 'Title', 'Description', 'RepositoryUrl', 'SpecifiedIssueUrl'];
  const definitions = Object.fromEntries(names.map(name => [name, { name, id: name, isDeleted: false }]));
  const base = Object.fromEntries(names.map(name => [`custom:${name}`, name]));
  const map = url => mapMetadata({ ...base, 'custom:SpecifiedIssueUrl': url }, definitions, { createDate: 0 }, 1, '2026-10-05T00:00:00Z');
  assert.equal(map('https://github.com/o/r/issues/7').SpecifiedIssueUrl, 'https://github.com/o/r/issues/7');
  assert.equal(map('  ').SpecifiedIssueUrl, undefined);
  assert.throws(() => map('https://example.com/issues/7'));
});

function fixture() {
  const columns = ['Id', 'Platform', 'Url', 'PlatformId', 'Description', 'Color', 'DisplayOrder']
    .map(name => ({ name, id: name, type: name === 'Platform' ? 'title' : name === 'Color' ? 'select' : 'rich-text',
      data: { options: [{ id: 'blue', value: '#123456' }] } }));
  const values = { Id: 1, Url: 'https://example.com', PlatformId: 'example', Description: 'Description', Color: 'blue', DisplayOrder: 1 };
  return { blocks: {
    db: { 'sys:flavour': 'affine:database', 'sys:children': ['row'], 'prop:columns': columns,
      'prop:cells': { row: Object.fromEntries(Object.entries(values).map(([key, value]) => [key, { value }])) } },
    row: { 'prop:text': 'Platform' },
  } };
}

test('maps row titles and select option labels with documented active default', () => {
  const result = mapBookmarks(fixture(), 'db', { createDate: 0 }, true)[0];
  assert.equal(result.Platform, 'Platform');
  assert.equal(result.Color, '#123456');
  assert.equal(result.IsActive, true);
});

test('rejects missing fields and duplicate identifiers', () => {
  const data = fixture();
  delete data.blocks.db['prop:cells'].row.Id;
  assert.throws(() => mapBookmarks(data, 'db', { createDate: 0 }, true));
  const duplicate = fixture();
  duplicate.blocks.db['sys:children'].push('row');
  assert.throws(() => mapBookmarks(duplicate, 'db', { createDate: 0 }, true));
});

test('hash detects changes without relying on source timestamps', () => {
  assert.notEqual(contentHash({ Title: 'a' }), contentHash({ Title: 'b' }));
});

function toolsFixture() {
  const columns = [
    { name: 'Id', id: 'col_id', type: 'number' },
    { name: 'Name', id: 'col_name', type: 'title' },
    { name: 'Description', id: 'col_desc', type: 'rich-text' },
    { name: 'Category', id: 'col_cat', type: 'select', data: { options: [{ id: 'opt_sc', value: 'SearchCollect' }] } },
    { name: 'DirectUrl', id: 'col_url', type: 'rich-text' },
    { name: 'RepositoryUrl', id: 'col_repo', type: 'rich-text' },
    { name: 'IssueUrl', id: 'col_issue', type: 'rich-text' },
    { name: 'DisplayOrder', id: 'col_order', type: 'number' },
    { name: 'IsActive', id: 'col_active', type: 'checkbox' },
  ];
  const values = {
    col_id: { value: 10 },
    col_desc: { value: 'Code Search tool' },
    col_cat: { value: 'opt_sc' },
    col_url: { value: 'http://localhost:5000' },
    col_repo: { value: 'https://github.com/org/repo' },
    col_issue: { value: 'https://github.com/org/repo/issues/1' },
    col_order: { value: 2 },
    col_active: { value: true },
  };
  return {
    blocks: {
      db: {
        'sys:flavour': 'affine:database',
        'prop:title': 'Tools',
        'sys:children': ['row1'],
        'prop:columns': columns,
        'prop:cells': { row1: values },
      },
      row1: { 'prop:text': 'Search Tool', 'prop:meta:createdAt': '2026-01-01T00:00:00.000Z' },
    },
  };
}

test('maps tools database table columns and select options correctly', () => {
  const result = mapTools(toolsFixture(), 'db', { createDate: 0 }, true)[0];
  assert.equal(result.Id, 10);
  assert.equal(result.Name, 'Search Tool');
  assert.equal(result.Category, 'SearchCollect');
  assert.equal(result.DirectUrl, 'http://localhost:5000');
  assert.equal(result.RepositoryUrl, 'https://github.com/org/repo');
  assert.equal(result.IssueUrl, 'https://github.com/org/repo/issues/1');
  assert.equal(result.DisplayOrder, 2);
  assert.equal(result.IsActive, true);
});

test('auto-discovers database block titled Tools when blockId is not passed', () => {
  const result = mapTools(toolsFixture(), undefined, { createDate: 0 }, true)[0];
  assert.equal(result.Id, 10);
  assert.equal(result.Name, 'Search Tool');
});

test('finds Tools only among explicit active members of the configured data collection, without Kind', () => {
  const root = {
    meta: { pages: [
      { id: 'outside', title: 'Tools' },
      { id: 'trashed', title: 'Tools', trash: true },
      { id: 'tools', title: 'Tools', createDate: Date.parse('2026-01-01') },
    ] },
    setting: { collections: [{ id: 'production', name: 'Production Data Set', allowList: ['tools', 'trashed'] }] },
  };
  assert.equal(findToolsPage(root, 'production').id, 'tools');
  assert.throws(() => findToolsPage(root, 'production', 'outside'), /explicit member/);
  assert.throws(() => findToolsPage(root, 'production', 'trashed'), /explicit member/);
  root.meta.pages.push({ id: 'second', title: 'Tools' });
  root.setting.collections[0].allowList.push('second');
  assert.throws(() => findToolsPage(root, 'production'), /More than one Tools page/);
  assert.equal(findToolsPage(root, 'production', 'tools').id, 'tools');
  root.meta.pages[2].title = 'Renamed Tools';
  assert.equal(findToolsPage(root, 'production', 'tools').id, 'tools');
  assert.equal(findToolsPage({ ...root, meta: { pages: [] } }, 'production'), undefined);
});

test('selects Tools by block title rather than the first unrelated database, and rejects ambiguity', () => {
  const fixture = toolsFixture();
  fixture.blocks = { other: { 'sys:flavour': 'affine:database', 'prop:title': 'Other' }, ...fixture.blocks };
  assert.equal(mapTools(fixture, undefined, { createDate: 0 })[0].Id, 10);
  fixture.blocks.second = { ...fixture.blocks.db };
  assert.throws(() => mapTools(fixture, undefined, { createDate: 0 }), /More than one database/);
  assert.equal(mapTools(fixture, 'db', { createDate: 0 })[0].Id, 10);
  delete fixture.blocks.second;
  fixture.blocks.db['prop:title'] = 'Renamed';
  assert.throws(() => mapTools(fixture, undefined, { createDate: 0 }), /Missing Tools/);
  assert.equal(mapTools(fixture, 'db', { createDate: 0 })[0].Id, 10);
});

test('decodes real CRDT title/text, number and link columns for the three production tools', () => {
  const doc = new Y.Doc();
  const blocks = doc.getMap('blocks');
  const db = new Y.Map();
  blocks.set('db', db);
  db.set('sys:flavour', 'affine:database');
  db.set('prop:title', new Y.Text('Tools'));
  db.set('prop:columns', Y.Array.from([
    { id: 'name', name: 'Name', type: 'title' },
    { id: 'id', name: 'Id', type: 'number' },
    { id: 'category', name: 'Category', type: 'rich-text' },
    { id: 'url', name: 'DirectUrl', type: 'link' },
    { id: 'description', name: 'Description', type: 'rich-text' },
  ]));
  const samples = [
    ['PARA Bridge', 'ObserveWatching', 'https://quocchungthan.github.io/para-bridge/'],
    ['Production Core Aids', 'ObserveWatching', 'https://quocchungthan.github.io/tools-w-productioncore-aids/'],
    ['Exploring Local', 'SearchCollect', 'https://quocchungthan.github.io/tools-s-exploring-local/'],
  ];
  const cells = new Y.Map();
  db.set('prop:cells', cells);
  db.set('sys:children', Y.Array.from(samples.map((_, index) => `row${index}`)));
  samples.forEach(([name, category, url], index) => {
    const row = new Y.Map();
    blocks.set(`row${index}`, row);
    row.set('prop:text', new Y.Text(name));
    const values = new Y.Map();
    cells.set(`row${index}`, values);
    for (const [key, value] of Object.entries({
      id: index + 1, category: new Y.Text(category), url, description: new Y.Text(''),
    })) {
      const cell = new Y.Map();
      values.set(key, cell);
      cell.set('value', value);
    }
  });
  const decoded = decodeDocument([{ blob: Y.encodeStateAsUpdate(doc) }]);
  const result = mapTools(decoded, undefined, { createDate: Date.parse('2026-01-01') });
  assert.deepEqual(result.map(row => [row.Name, row.Category, row.DirectUrl]), samples);
  assert.deepEqual(result.map(row => row.Id), [1, 2, 3]);
  assert.ok(result.every(row => row.IsActive && row.DisplayOrder === 0 && row.Description === ''));
  decoded.blocks.db['prop:columns'][0].type = 'rich-text';
  decoded.blocks.db['sys:children'] = ['row0'];
  decoded.blocks.db['prop:cells'].row0.name = { value: 'Text Name' };
  assert.equal(mapTools(decoded, undefined, { createDate: 0 })[0].Name, 'Text Name');
  doc.destroy();
});

test('skipped-page reports expose reasons without leaking page titles or IDs in CI', () => {
  const page = { id: 'private-id', title: 'Private title', kind: 'Experience',
    reason: 'Invalid Experience.StartDate (page private-id): expected YYYY-MM-DD' };
  const redacted = skippedPageDiagnostic(page, false);
  assert.match(redacted, /Experience.StartDate/);
  assert.ok(!redacted.includes(page.id) && !redacted.includes(page.title));
  assert.match(skippedPageDiagnostic(page, true), /private-id/);
});
