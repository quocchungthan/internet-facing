import * as Y from 'yjs';

const doc = new Y.Doc();
const pages = new Y.Array();
doc.getMap('meta').set('pages', pages);
for (const [id, title, trash] of [
  ['doc-1', 'Alpha note', false], ['doc-2', 'Beta note', false],
  ['doc-3', 'Hidden note', false], ['trash', 'Trash note', true], ['blocked', 'Blocked note', false]
]) pages.push([new Y.Map([['id', id], ['title', title], ['trash', trash]])]);
const root = Buffer.from(Y.encodeStateAsUpdate(doc)).toString('base64');
doc.destroy();

const note = new Y.Doc();
const page = new Y.Map([['sys:flavour', 'affine:page'], ['sys:children', Y.Array.from(['text'])],
  ['prop:title', new Y.Text('Alpha note')]]);
note.getMap('blocks').set('page', page);
const paragraph = new Y.Map([['sys:flavour', 'affine:paragraph'], ['sys:children', new Y.Array()],
  ['prop:text', new Y.Text('Original text')]]);
note.getMap('blocks').set('text', paragraph);
const snapshot = Buffer.from(Y.encodeStateAsUpdate(note)).toString('base64');
const vector = Y.encodeStateVector(note);
const text = paragraph.get('prop:text');
text.delete(0, text.length);
text.insert(0, 'Latest private text');
const update = Buffer.from(Y.encodeStateAsUpdate(note, vector)).toString('base64');
note.destroy();
process.stdout.write(JSON.stringify({ root, snapshot, update }));
