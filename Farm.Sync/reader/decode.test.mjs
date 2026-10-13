import assert from 'node:assert/strict';
import { test } from 'node:test';
import * as Y from 'yjs';
import { decodeDocument, mapBookmarks, mapMetadata, mapTools, contentHash } from './decode.mjs';

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

test('auto-discovers affine:database block when blockId is not passed to mapTools', () => {
  const result = mapTools(toolsFixture(), undefined, { createDate: 0 }, true)[0];
  assert.equal(result.Id, 10);
  assert.equal(result.Name, 'Search Tool');
});
