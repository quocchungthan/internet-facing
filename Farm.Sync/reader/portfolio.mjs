import * as Y from 'yjs';

// Page-as-record mapping: page properties hold scalar values, the page body holds prose.

export const KINDS = ['Profile', 'Project', 'Experience', 'Gig', 'Skills', 'SocialLinks'];
const EMPLOYMENT_TYPES = ['FullTime', 'PartTime', 'Contract', 'Freelance', 'Internship'];
const WORK_MODES = ['OnSite', 'Hybrid', 'Remote'];

export function loadYDoc(records) {
  if (!records.length) throw new Error('Missing AFFiNE document snapshot/updates');
  const doc = new Y.Doc();
  for (const record of records) Y.applyUpdate(doc, record.blob);
  return doc;
}

function inline(text, titleOf) {
  if (!text?.toDelta) return '';
  return text.toDelta().map(op => {
    if (typeof op.insert !== 'string') return '';
    const a = op.attributes ?? {};
    let value = op.insert;
    if (a.reference?.pageId) value = titleOf(a.reference.pageId) ?? value.trim();
    if (a.code) value = `\`${value}\``;
    if (a.bold) value = `**${value}**`;
    if (a.link) value = `[${value}](${a.link})`;
    return value;
  }).join('');
}

function node(blocks, blockId, titleOf) {
  const block = blocks.get(blockId);
  if (!block) return null;
  const flavour = block.get('sys:flavour');
  if (!['affine:paragraph', 'affine:list', 'affine:code'].includes(flavour)) return null;
  const text = inline(block.get('prop:text'), titleOf);
  return {
    flavour,
    type: block.get('prop:type') ?? 'text',
    language: block.get('prop:language'),
    text,
    children: (block.get('sys:children')?.toArray() ?? []).map(id => node(blocks, id, titleOf)).filter(Boolean),
  };
}

// Top-level content blocks of a page (everything inside its note blocks).
export function bodyNodes(ydoc, titleOf = () => undefined) {
  const blocks = ydoc.getMap('blocks');
  const page = [...blocks.values()].find(b => b.get('sys:flavour') === 'affine:page');
  if (!page) throw new Error('AFFiNE document has no page block');
  const result = [];
  for (const id of page.get('sys:children').toArray()) {
    const note = blocks.get(id);
    if (note?.get('sys:flavour') !== 'affine:note') continue;
    for (const childId of note.get('sys:children').toArray()) {
      const n = node(blocks, childId, titleOf);
      if (n) result.push(n);
    }
  }
  return result;
}

function render(nodes, indent = '') {
  const lines = [];
  let numbered = 0;
  nodes.forEach((n, index) => {
    const previous = nodes[index - 1];
    const isList = n.flavour === 'affine:list';
    numbered = isList && n.type === 'numbered' ? numbered + 1 : 0;
    let text;
    if (n.flavour === 'affine:code') text = `\`\`\`${n.language ?? ''}\n${n.text}\n\`\`\``;
    else if (isList) {
      const marker = n.type === 'numbered' ? `${numbered}. ` : n.type === 'todo' ? '- [ ] ' : '- ';
      text = marker + n.text;
    } else if (/^h[1-6]$/.test(n.type)) text = `${'#'.repeat(Number(n.type[1]))} ${n.text}`;
    else if (n.type === 'quote') text = `> ${n.text}`;
    else text = n.text;
    if (!isList && !text.trim()) return;
    const tight = isList && previous?.flavour === 'affine:list';
    if (lines.length && !tight) lines.push('');
    lines.push(...text.split('\n').map(line => indent + line));
    if (n.children.length) {
      const rendered = render(n.children, indent + '  ');
      if (rendered) lines.push(rendered);
    }
  });
  return lines.join('\n');
}

export function markdown(nodes) {
  return render(nodes).trim();
}

export function bodyMarkdown(ydoc, titleOf) {
  return markdown(bodyNodes(ydoc, titleOf));
}

// ---- properties ----------------------------------------------------------

export function propertyReader(properties, definitions, pageId, kind) {
  const values = properties[pageId] ?? {};
  const read = name => {
    const matches = Object.values(definitions).filter(d => d.name === name && !d.isDeleted);
    if (matches.length > 1) throw new Error(`Duplicate active property ${name}`);
    return matches.length ? values[`custom:${matches[0].id}`] : undefined;
  };
  const where = name => `${kind}.${name} (page ${pageId})`;
  const text = (name, { required = false } = {}) => {
    const value = read(name);
    if (value == null || (typeof value === 'string' && !value.trim())) {
      if (required) throw new Error(`Missing ${where(name)}`);
      return null;
    }
    if (typeof value !== 'string') throw new Error(`Invalid ${where(name)}: expected text`);
    return value.trim();
  };
  const bool = name => {
    const value = read(name);
    if (value == null) return false;
    if (typeof value !== 'boolean') throw new Error(`Invalid ${where(name)}: expected checkbox`);
    return value;
  };
  const int = name => {
    const value = read(name);
    if (value == null || value === '') return 0;
    const number = typeof value === 'string' ? Number(value) : value;
    if (!Number.isSafeInteger(number) || number < 0) throw new Error(`Invalid ${where(name)}: expected a non-negative integer`);
    return number;
  };
  const date = (name, { required = false } = {}) => {
    const value = text(name, { required });
    if (value === null) return null;
    if (!/^\d{4}-\d{2}-\d{2}$/.test(value) || Number.isNaN(Date.parse(value))) throw new Error(`Invalid ${where(name)}: expected YYYY-MM-DD`);
    return value;
  };
  const oneOf = (name, allowed) => {
    const value = text(name, { required: true });
    if (!allowed.includes(value)) throw new Error(`Invalid ${where(name)}: expected one of ${allowed.join(', ')}`);
    return value;
  };
  const list = name => (text(name) ?? '').split(',').map(s => s.trim()).filter(Boolean);
  return { read, text, bool, int, date, oneOf, list };
}

export function slugify(value) {
  return value.toLowerCase().normalize('NFKD').replace(/[\u0300-\u036f]/g, '')
    .replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, '');
}

function nonEmpty(value, name) {
  if (!value) throw new Error(`Missing ${name}`);
  return value;
}

// ---- page mappers --------------------------------------------------------

export function mapProfile(p, pageId, body) {
  return {
    SourceDocId: pageId,
    DisplayName: p.text('DisplayName', { required: true }),
    Headline: p.text('Headline', { required: true }),
    Bio: body || null,
    Location: p.text('Location'),
    ContactEmail: p.text('ContactEmail'),
    ProfileImageUrl: p.text('ProfileImageUrl'),
    ResumeUrl: p.text('ResumeUrl'),
  };
}

export function mapProject(p, pageId, title, body) {
  return {
    SourceDocId: pageId,
    Title: nonEmpty(title?.trim(), `Project title (page ${pageId})`),
    Slug: p.text('Slug') ?? slugify(title),
    Summary: p.text('Summary', { required: true }),
    Description: body || null,
    DemoUrl: p.text('DemoUrl'),
    SourceUrl: p.text('SourceUrl'),
    Role: p.text('Role'),
    Organization: p.text('Organization'),
    StartDate: p.date('StartDate'),
    EndDate: p.date('EndDate'),
    IsFeatured: p.bool('IsFeatured'),
    DisplayOrder: p.int('DisplayOrder'),
    Skills: p.list('Skills'),
  };
}

export function mapExperience(p, pageId, title, body) {
  const row = {
    SourceDocId: pageId,
    Company: p.text('Company', { required: true }),
    JobTitle: nonEmpty(title?.trim(), `Experience title (page ${pageId})`),
    Description: body || null,
    EmploymentType: p.oneOf('EmploymentType', EMPLOYMENT_TYPES),
    WorkMode: p.oneOf('WorkMode', WORK_MODES),
    StartDate: p.date('StartDate', { required: true }),
    EndDate: p.date('EndDate'),
    IsCurrent: p.bool('IsCurrent'),
    DisplayOrder: p.int('DisplayOrder'),
  };
  if (row.IsCurrent && row.EndDate) throw new Error(`Experience ${pageId}: a current role cannot have an EndDate`);
  if (row.EndDate && row.EndDate < row.StartDate) throw new Error(`Experience ${pageId}: EndDate precedes StartDate`);
  return row;
}

// Gig body: "## Problem", "## Outcome" and a "## Steps" numbered list
// (item text = step title, nested paragraphs = step description).
export function mapGig(p, pageId, title, nodes) {
  const sections = new Map();
  let current = null;
  for (const n of nodes) {
    if (n.flavour === 'affine:paragraph' && /^h[1-6]$/.test(n.type)) {
      current = n.text.trim().toLowerCase();
      if (sections.has(current)) throw new Error(`Gig ${pageId}: duplicate section "${n.text}"`);
      sections.set(current, []);
    } else if (current) sections.get(current).push(n);
    else if (n.text.trim()) throw new Error(`Gig ${pageId}: text before the first heading; use "## Problem"`);
  }
  for (const name of sections.keys())
    if (!['problem', 'outcome', 'steps'].includes(name)) throw new Error(`Gig ${pageId}: unknown section "${name}"`);
  const steps = (sections.get('steps') ?? []).map((n, index) => {
    if (n.flavour !== 'affine:list') throw new Error(`Gig ${pageId}: the Steps section may only contain a list`);
    return { StepNumber: index + 1, Title: nonEmpty(n.text.trim(), `Gig ${pageId} step ${index + 1} title`), Description: markdown(n.children) || null };
  });
  return {
    SourceDocId: pageId,
    Title: nonEmpty(title?.trim(), `Gig title (page ${pageId})`),
    Slug: p.text('Slug') ?? slugify(title),
    Category: p.text('Category', { required: true }),
    Summary: p.text('Summary', { required: true }),
    Problem: markdown(sections.get('problem') ?? []) || null,
    Outcome: markdown(sections.get('outcome') ?? []) || null,
    SolvedOn: p.date('SolvedOn'),
    DisplayOrder: p.int('DisplayOrder'),
    Skills: p.list('Skills'),
    Steps: steps,
  };
}

// ---- database blocks (Skills.Data, SocialLinks.Data) ----------------------

export function readTable(ydoc, title, titleOf = () => undefined) {
  const blocks = ydoc.getMap('blocks');
  const database = [...blocks.values()].find(b =>
    b.get('sys:flavour') === 'affine:database' && b.get('prop:title')?.toString() === title);
  if (!database) throw new Error(`Missing database block "${title}"`);
  const columns = database.get('prop:columns').toJSON();
  const cells = database.get('prop:cells').toJSON();
  return database.get('sys:children').toArray().map(rowId => {
    const row = {};
    for (const column of columns) {
      let value;
      if (column.type === 'title') value = inline(blocks.get(rowId)?.get('prop:text'), titleOf);
      else value = cells[rowId]?.[column.id]?.value;
      if (column.type === 'select' && value != null)
        value = column.data?.options?.find(o => o.id === value)?.value;
      row[column.name] = typeof value === 'string' ? value.trim() : value;
    }
    return row;
  });
}

function requiredCell(value, name) {
  if (typeof value !== 'string' || !value.trim()) throw new Error(`Missing/invalid ${name}`);
  return value;
}

function order(value, name) {
  const number = value == null || value === '' ? 0 : Number(value);
  if (!Number.isSafeInteger(number) || number < 0) throw new Error(`Invalid ${name}`);
  return number;
}

export function mapSkills(ydoc, titleOf) {
  return readTable(ydoc, 'Skills.Data', titleOf).map(row => ({
    Name: requiredCell(row.Name, 'Skills.Name'),
    Category: requiredCell(row.Category, `Skills.Category (${row.Name})`),
    DisplayOrder: order(row.DisplayOrder, `Skills.DisplayOrder (${row.Name})`),
  }));
}

export function mapSocialLinks(ydoc, titleOf) {
  return readTable(ydoc, 'SocialLinks.Data', titleOf).map(row => ({
    Label: requiredCell(row.Label, 'SocialLinks.Label'),
    Url: requiredCell(row.Url, `SocialLinks.Url (${row.Label})`),
    DisplayOrder: order(row.DisplayOrder, `SocialLinks.DisplayOrder (${row.Label})`),
  }));
}
