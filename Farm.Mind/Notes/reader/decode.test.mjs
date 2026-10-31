import assert from 'node:assert/strict';
import { test } from 'node:test';
import * as Y from 'yjs';
import { decode } from './decode.mjs';

function block(doc, id, flavour, children = [], props = {}) {
  const value = new Y.Map();
  doc.getMap('blocks').set(id, value);
  value.set('sys:flavour', flavour);
  value.set('sys:children', Y.Array.from(children));
  for (const [key, prop] of Object.entries(props)) value.set(`prop:${key}`, prop);
  return value;
}

const snapshot = doc => Buffer.from(Y.encodeStateAsUpdate(doc)).toString('base64');

test('merges pending edits and renders Page, heading, list and code in child order', () => {
  const doc = new Y.Doc();
  block(doc, 'page', 'affine:page', ['note'], { title: new Y.Text('My title') });
  block(doc, 'note', 'affine:note', ['heading', 'list', 'code']);
  block(doc, 'heading', 'affine:paragraph', [], { type: 'h2', text: new Y.Text('Before') });
  block(doc, 'list', 'affine:list', [], { type: 'todo', checked: true, text: new Y.Text('Done') });
  block(doc, 'code', 'affine:code', [], { language: 'js', text: new Y.Text('const x = "```";') });
  const initial = snapshot(doc);
  const vector = Y.encodeStateVector(doc);
  const heading = doc.getMap('blocks').get('heading').get('prop:text');
  heading.delete(0, heading.length);
  heading.insert(0, 'After');
  const update = Buffer.from(Y.encodeStateAsUpdate(doc, vector)).toString('base64');
  const result = decode({ root: false, updates: [initial, update] });
  assert.match(result.markdown, /# My title\n\n## After\n\n- \[x\] Done/);
  assert.match(result.markdown, /````js\nconst x = "```";\n````/);
  assert.deepEqual(result.unsupportedBlocks, []);
  doc.destroy();
});

test('root merges new pages and excludes trash without relying on workspace_pages rows', () => {
  const doc = new Y.Doc();
  const pages = new Y.Array();
  doc.getMap('meta').set('pages', pages);
  pages.push([new Y.Map([['id', 'doc-1'], ['title', 'First']])]);
  const initial = snapshot(doc);
  pages.push([new Y.Map([['id', 'doc-2'], ['title', 'Second']]),
    new Y.Map([['id', 'trash'], ['title', 'Deleted'], ['trash', true]])]);
  assert.deepEqual(decode({ root: true, updates: [initial, snapshot(doc)] }).pages,
    [{ docId: 'doc-1', title: 'First' }, { docId: 'doc-2', title: 'Second' }]);
  doc.destroy();
});

test('unsupported blocks and canvas elements are explicitly reported, not silently dropped', () => {
  const doc = new Y.Doc();
  block(doc, 'page', 'affine:page', ['database', 'surface', 'image']);
  block(doc, 'database', 'affine:database');
  block(doc, 'image', 'affine:image');
  block(doc, 'surface', 'affine:surface', [], { elements: new Y.Map([['shape-1', new Y.Map()]]) });
  const result = decode({ updates: [snapshot(doc)], root: false });
  assert.deepEqual(result.unsupportedBlocks, ['affine:database', 'affine:image', 'affine:surface']);
  assert.match(result.markdown, /Canvas elements not rendered/);
  doc.destroy();
});

test('corrupt, incomplete, cyclic or missing documents fail closed', () => {
  assert.throws(() => decode({ updates: ['AA=='], root: false }));
  assert.throws(() => decode({ updates: [], root: true }));
  const doc = new Y.Doc();
  block(doc, 'page', 'affine:page', ['page']);
  assert.throws(() => decode({ updates: [snapshot(doc)], root: false }));
  const vector = Y.encodeStateVector(doc);
  doc.getMap('blocks').get('page').set('prop:title', 'new');
  const orphan = Buffer.from(Y.encodeStateAsUpdate(doc, vector)).toString('base64');
  assert.throws(() => decode({ updates: [orphan], root: false }));
  doc.destroy();
});
