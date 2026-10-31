import * as Y from 'yjs';
import { pathToFileURL } from 'node:url';

const text = value => value instanceof Y.Text ? value.toString() : typeof value === 'string' ? value : '';

export function decode({ updates, root }) {
  const doc = new Y.Doc();
  try {
    for (const update of updates) Y.applyUpdate(doc, Buffer.from(update, 'base64'));
    if (doc.store.pendingStructs || doc.store.pendingDs) throw new Error('Incomplete Yjs state');
    if (root) {
      const pages = doc.getMap('meta').get('pages');
      if (!(pages instanceof Y.Array)) throw new Error('Missing workspace page metadata');
      const seen = new Set();
      const result = [];
      for (const page of pages.toArray()) {
        if (!(page instanceof Y.Map) || typeof page.get('id') !== 'string') throw new Error('Invalid page metadata');
        const id = page.get('id');
        if (seen.has(id)) throw new Error('Duplicate page metadata');
        seen.add(id);
        if (page.get('trash') !== true) result.push({ docId: id, title: text(page.get('title')) });
      }
      if (result.length > 10000) throw new Error('Workspace page limit exceeded');
      return { pages: result, markdown: '', unsupportedBlocks: [] };
    }

    const blocks = doc.getMap('blocks');
    const page = [...blocks.values()].find(block => block instanceof Y.Map && block.get('sys:flavour') === 'affine:page');
    if (!page || blocks.size > 100000) throw new Error('Missing or oversized block graph');
    const unsupported = new Set();
    const visited = new Set();
    const active = new Set();
    let characters = 0;
    const render = block => {
      if (!(block instanceof Y.Map)) throw new Error('Missing child block');
      if (active.has(block)) throw new Error('Cyclic block graph');
      if (visited.has(block)) return '';
      active.add(block);
      visited.add(block);
      const flavour = block.get('sys:flavour');
      const body = text(block.get('prop:text'));
      const type = block.get('prop:type');
      let line = '';
      switch (flavour) {
        case 'affine:page':
          line = text(block.get('prop:title')) ? `# ${text(block.get('prop:title'))}` : '';
          break;
        case 'affine:note':
          break;
        case 'affine:surface': {
          const elements = block.get('prop:elements');
          if (elements && (elements.size ?? Object.keys(elements).length) > 0) {
            unsupported.add('affine:surface');
            line = '[Canvas elements not rendered]';
          }
          break;
        }
        case 'affine:paragraph':
          line = /^h[1-6]$/.test(type) ? `${'#'.repeat(Number(type[1]))} ${body}` :
            type === 'quote' ? `> ${body.replaceAll('\n', '\n> ')}` : body;
          break;
        case 'affine:list':
          line = type === 'todo' ? `- [${block.get('prop:checked') ? 'x' : ' '}] ${body}` :
            type === 'numbered' ? `1. ${body}` : `- ${body}`;
          break;
        case 'affine:code': {
          const fence = '`'.repeat(Math.max(3, ...(body.match(/`+/g) ?? []).map(run => run.length + 1)));
          line = `${fence}${text(block.get('prop:language')).replace(/[\r\n`]/g, '')}\n${body}\n${fence}`;
          break;
        }
        case 'affine:divider':
          line = '---';
          break;
        default:
          unsupported.add(typeof flavour === 'string' ? flavour : 'unknown');
          line = `[Unsupported block: ${typeof flavour === 'string' ? flavour : 'unknown'}]`;
          if (body) line += `\n${body}`;
      }
      characters += line.length;
      if (characters > 1000000) throw new Error('Rendered text limit exceeded');
      const children = block.get('sys:children');
      if (children !== undefined && !(children instanceof Y.Array)) throw new Error('Invalid children');
      const rendered = (children?.toArray() ?? []).map(id => render(blocks.get(id)));
      active.delete(block);
      return [line, ...rendered].filter(Boolean).join('\n\n');
    };
    return { markdown: render(page), pages: [], unsupportedBlocks: [...unsupported].sort() };
  } finally {
    doc.destroy();
  }
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  try {
    let size = 0;
    const chunks = [];
    for await (const chunk of process.stdin) {
      size += chunk.length;
      if (size > 24 * 1024 * 1024) throw new Error('Input limit exceeded');
      chunks.push(chunk);
    }
    process.stdout.write(JSON.stringify(decode(JSON.parse(Buffer.concat(chunks).toString('utf8')))));
  } catch {
    process.stderr.write('Stored Note document could not be decoded.\n');
    process.exitCode = 1;
  }
}
