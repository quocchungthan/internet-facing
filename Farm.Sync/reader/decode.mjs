import { createHash } from 'node:crypto';
import * as Y from 'yjs';

export function decodeDocument(records) {
  if (!records.length) throw new Error('Missing AFFiNE document snapshot/updates');
  const doc = new Y.Doc();
  try {
    for (const record of records) Y.applyUpdate(doc, record.blob);
    for (const [key] of doc.share) doc.getMap(key);
    return doc.toJSON();
  } finally {
    doc.destroy();
  }
}

function requiredText(value, name) {
  if (typeof value !== 'string' || !value.trim()) throw new Error(`Missing/invalid ${name}`);
  return value;
}

function integer(value, name) {
  const number = typeof value === 'string' && value.trim() ? Number(value) : value;
  if (!Number.isSafeInteger(number)) throw new Error(`Invalid integer ${name}`);
  return number;
}

function timestamp(value, name) {
  const date = new Date(value);
  if (value == null || !Number.isFinite(date.getTime())) throw new Error(`Invalid timestamp ${name}`);
  return date.toISOString();
}

export function mapMetadata(properties, definitions, page, entityId, updatedAtUtc) {
  const result = {
    Id: integer(entityId, 'Meta.Id'),
    CreatedAtUtc: timestamp(page.createDate, 'Meta.createDate'),
    UpdatedAtUtc: timestamp(updatedAtUtc, 'Meta.updatedAtUtc'),
  };
  for (const name of ['DeveloperName', 'Title', 'Description', 'RepositoryUrl']) {
    const matches = Object.values(definitions).filter(x => x.name === name && !x.isDeleted);
    if (matches.length !== 1) throw new Error(`Expected one active Meta property named ${name}`);
    result[name] = requiredText(properties[`custom:${matches[0].id}`], `Meta.${name}`);
  }
  const issue = Object.values(definitions).filter(x => x.name === 'SpecifiedIssueUrl' && !x.isDeleted);
  if (issue.length > 1) throw new Error('Expected at most one active Meta property named SpecifiedIssueUrl');
  const issueUrl = issue.length ? properties[`custom:${issue[0].id}`] : undefined;
  if (typeof issueUrl === 'string' && issueUrl.trim()) {
    if (!/^https:\/\/github\.com\/[\w.-]+\/[\w.-]+\/issues\/\d+$/.test(issueUrl.trim()))
      throw new Error('Meta.SpecifiedIssueUrl must be a GitHub issue link');
    result.SpecifiedIssueUrl = issueUrl.trim();
  }
  if (result.Id <= 0) throw new Error('Meta.Id must be positive');
  return result;
}

export function mapBookmarks(document, blockId, page, defaultIsActive) {
  const blocks = document.blocks;
  const database = blocks?.[blockId];
  if (database?.['sys:flavour'] !== 'affine:database') throw new Error(`Missing Feeds database block ${blockId}`);
  const columns = database['prop:columns'];
  if (!Array.isArray(columns)) throw new Error('Feeds database columns are missing');
  const byName = new Map();
  for (const column of columns) {
    if (byName.has(column.name)) throw new Error(`Duplicate Feeds column ${column.name}`);
    byName.set(column.name, column);
  }
  const ids = new Set();
  return database['sys:children'].map(rowId => {
    const row = blocks[rowId];
    if (!row) throw new Error(`Missing Feeds row ${rowId}`);
    const cells = database['prop:cells']?.[rowId] ?? {};
    function value(name, optional = false) {
      const column = byName.get(name);
      if (!column) {
        if (optional) return undefined;
        throw new Error(`Missing Feeds column ${name}`);
      }
      let v = column.type === 'title' ? row['prop:text'] : cells[column.id]?.value;
      if (column.type === 'select' && v != null) {
        const option = column.data?.options?.find(option => option.id === v);
        if (!option) throw new Error(`Unknown option for Feeds.${name} in row ${rowId}`);
        v = option.value;
      }
      return v;
    }
    const id = integer(value('Id'), 'Feeds.Id');
    if (id <= 0 || ids.has(id)) throw new Error(`Invalid/duplicate Feeds.Id ${id}`);
    ids.add(id);
    const active = value('IsActive', true);
    if (active !== undefined && typeof active !== 'boolean') throw new Error(`Invalid Feeds.IsActive for ${id}`);
    return {
      Id: id,
      Platform: requiredText(value('Platform'), 'Feeds.Platform'),
      Url: requiredText(value('Url'), 'Feeds.Url'),
      PlatformId: requiredText(value('PlatformId'), 'Feeds.PlatformId'),
      Description: requiredText(value('Description'), 'Feeds.Description'),
      Color: requiredText(value('Color'), 'Feeds.Color'),
      DisplayOrder: integer(value('DisplayOrder'), 'Feeds.DisplayOrder'),
      IsActive: active ?? defaultIsActive,
      CreatedAtUtc: timestamp(row['prop:meta:createdAt'] ?? page.createDate, 'Feeds.CreatedAtUtc'),
    };
  });
}

export function mapTools(document, blockId, page, defaultIsActive = true) {
  const blocks = document.blocks;
  const database = blockId ? blocks?.[blockId] : Object.values(blocks ?? {}).find(b => b?.['sys:flavour'] === 'affine:database');
  if (database?.['sys:flavour'] !== 'affine:database') throw new Error(`Missing Tools database block ${blockId ?? ''}`);
  const columns = database['prop:columns'];
  if (!Array.isArray(columns)) throw new Error('Tools database columns are missing');
  const byName = new Map();
  for (const column of columns) {
    if (byName.has(column.name)) throw new Error(`Duplicate Tools column ${column.name}`);
    byName.set(column.name, column);
  }
  const ids = new Set();
  return (database['sys:children'] ?? []).map(rowId => {
    const row = blocks[rowId];
    if (!row) throw new Error(`Missing Tools row ${rowId}`);
    const cells = database['prop:cells']?.[rowId] ?? {};
    function value(name, optional = false) {
      const column = byName.get(name);
      if (!column) {
        if (optional) return undefined;
        throw new Error(`Missing Tools column ${name}`);
      }
      let v = column.type === 'title' ? row['prop:text'] : cells[column.id]?.value;
      if (column.type === 'select' && v != null) {
        const option = column.data?.options?.find(option => option.id === v);
        if (!option) throw new Error(`Unknown option for Tools.${name} in row ${rowId}`);
        v = option.value;
      }
      return v;
    }
    const id = integer(value('Id'), 'Tools.Id');
    if (id <= 0 || ids.has(id)) throw new Error(`Invalid/duplicate Tools.Id ${id}`);
    ids.add(id);
    const active = value('IsActive', true);
    if (active !== undefined && typeof active !== 'boolean') throw new Error(`Invalid Tools.IsActive for ${id}`);
    const rawName = byName.has('Name') ? value('Name') : (row['prop:text'] ?? value('Title', true));
    const repo = value('RepositoryUrl', true);
    const issue = value('IssueUrl', true);
    return {
      Id: id,
      Name: requiredText(rawName, 'Tools.Name'),
      Description: requiredText(value('Description'), 'Tools.Description'),
      Category: requiredText(value('Category'), 'Tools.Category'),
      DirectUrl: requiredText(value('DirectUrl'), 'Tools.DirectUrl'),
      RepositoryUrl: typeof repo === 'string' && repo.trim() ? repo.trim() : undefined,
      IssueUrl: typeof issue === 'string' && issue.trim() ? issue.trim() : undefined,
      DisplayOrder: integer(value('DisplayOrder', true) ?? 0, 'Tools.DisplayOrder'),
      IsActive: active ?? defaultIsActive,
      CreatedAtUtc: timestamp(row['prop:meta:createdAt'] ?? page.createDate, 'Tools.CreatedAtUtc'),
      UpdatedAtUtc: timestamp(row['prop:meta:updatedAt'] ?? row['prop:meta:createdAt'] ?? page.createDate, 'Tools.UpdatedAtUtc'),
    };
  });
}

export function contentHash(value) {
  return createHash('sha256').update(JSON.stringify(value)).digest('hex');
}
