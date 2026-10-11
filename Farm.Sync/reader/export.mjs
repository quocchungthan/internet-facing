import pg from 'pg';
import { contentHash, decodeDocument, mapBookmarks, mapMetadata } from './decode.mjs';
import {
  KINDS, bodyMarkdown, bodyNodes, loadYDoc, mapExperience, mapGig, mapProfile, mapProject,
  mapSkills, mapSocialLinks, propertyReader,
} from './portfolio.mjs';

function required(name) {
  const value = process.env[name];
  if (!value?.trim()) throw new Error(`${name} is required`);
  return value;
}

const workspaceId = required('AFFINE_WORKSPACE_ID');
const metaId = required('AFFINE_META_DOC_ID');
const feedsId = required('AFFINE_FEEDS_DOC_ID');
const propertiesId = `db$${workspaceId}$docProperties`;
const definitionsId = `db$${workspaceId}$docCustomPropertyInfo`;
const ids = [workspaceId, metaId, feedsId, propertiesId, definitionsId];
const client = new pg.Client({
  connectionString: required('AFFINE_DATABASE_URL'),
  connectionTimeoutMillis: 15000,
  statement_timeout: 30000,
});

const SQL = `
    SELECT guid AS id, blob, updated_at AS timestamp FROM snapshots
    WHERE workspace_id = $1 AND guid = ANY($2::varchar[])
    UNION ALL
    SELECT guid AS id, blob, created_at AS timestamp FROM updates
    WHERE workspace_id = $1 AND guid = ANY($2::varchar[])
    ORDER BY timestamp`;

try {
  await client.connect();
  await client.query('BEGIN TRANSACTION ISOLATION LEVEL REPEATABLE READ READ ONLY');
  let rows = (await client.query(SQL, [workspaceId, ids])).rows;
  // Pages of the optional Portfolio/Gigs collections are read in the same snapshot.
  const extraCollectionIds = ['AFFINE_PORTFOLIO_COLLECTION_ID', 'AFFINE_GIGS_COLLECTION_ID']
    .map(name => process.env[name]?.trim()).filter(Boolean);
  const rootRecords = rows.filter(row => row.id === workspaceId);
  const rootForPages = rootRecords.length ? decodeDocument(rootRecords) : {};
  const pageIds = [...new Set(extraCollectionIds.flatMap(collectionId => {
    const found = rootForPages.setting?.collections?.find(c => c.id === collectionId);
    if (!found) throw new Error(`Collection ${collectionId} was not found`);
    return found.allowList ?? [];
  }))].filter(pageId => !ids.includes(pageId));
  if (pageIds.length) rows = rows.concat((await client.query(SQL, [workspaceId, pageIds])).rows);
  await client.query('COMMIT');
  const records = id => rows.filter(row => row.id === id);
  const decoded = new Map(ids.map(id => [id, decodeDocument(records(id))]));
  const root = decoded.get(workspaceId);
  const pages = root.meta?.pages;
  if (!Array.isArray(pages)) throw new Error('Workspace page metadata is missing');
  const collectionId = required('AFFINE_COLLECTION_ID');
  const collection = root.setting?.collections?.find(c => c.id === collectionId);
  if (!collection || ![metaId, feedsId].every(id => collection.allowList?.includes(id))) {
    throw new Error('Configured Meta/Feeds documents must be explicit members of the configured collection');
  }
  function page(id) {
    const result = pages.find(page => page.id === id && !page.trash);
    if (!result) throw new Error(`Configured page ${id} is missing or trashed`);
    return result;
  }
  function updated(dependencies) {
    const timestamps = dependencies.flatMap(id => records(id).map(r => r.timestamp.getTime()));
    return new Date(Math.max(...timestamps)).toISOString();
  }
  const metaUpdated = updated([metaId, propertiesId, definitionsId, workspaceId]);
  const feedsUpdated = updated([feedsId, workspaceId]);
  const active = process.env.AFFINE_DEFAULT_IS_ACTIVE ?? 'true';
  if (!['true', 'false'].includes(active)) throw new Error('AFFINE_DEFAULT_IS_ACTIVE must be true or false');
  const metadata = [mapMetadata(
    decoded.get(propertiesId)[metaId],
    decoded.get(definitionsId),
    page(metaId),
    Number(process.env.AFFINE_META_ENTITY_ID ?? '1'),
    metaUpdated,
  )];
  const bookmarks = mapBookmarks(decoded.get(feedsId), required('AFFINE_FEEDS_BLOCK_ID'), page(feedsId), active === 'true');
  const documents = [
    { id: metaId, updatedAtUtc: metaUpdated, metadata, bookmarks: [], contentHash: contentHash(metadata) },
    { id: feedsId, updatedAtUtc: feedsUpdated, metadata: [], bookmarks, contentHash: contentHash(bookmarks) },
  ];
  const titleOf = pageId => pages.find(page => page.id === pageId)?.title;
  for (const pageId of pageIds) {
    const meta = pages.find(page => page.id === pageId);
    if (!meta || meta.trash) continue;
    const properties = decodeDocument(records(propertiesId))[pageId] ?? {};
    const kind = propertyReader({ [pageId]: properties }, decoded.get(definitionsId), pageId, '').read('Kind');
    if (kind == null || kind === '') continue;
    if (!KINDS.includes(kind)) throw new Error(`Page ${pageId} has unknown Kind "${kind}"; expected one of ${KINDS.join(', ')}`);
    const p = propertyReader({ [pageId]: properties }, decoded.get(definitionsId), pageId, kind);
    const ydoc = loadYDoc(records(pageId));
    const rowsOf = { profiles: [], projects: [], experiences: [], skills: [], socialLinks: [], gigs: [] };
    try {
      if (kind === 'Profile') rowsOf.profiles.push(mapProfile(p, pageId, bodyMarkdown(ydoc, titleOf)));
      else if (kind === 'Project') rowsOf.projects.push(mapProject(p, pageId, meta.title, bodyMarkdown(ydoc, titleOf)));
      else if (kind === 'Experience') rowsOf.experiences.push(mapExperience(p, pageId, meta.title, bodyMarkdown(ydoc, titleOf)));
      else if (kind === 'Gig') rowsOf.gigs.push(mapGig(p, pageId, meta.title, bodyNodes(ydoc, titleOf)));
      else if (kind === 'Skills') rowsOf.skills = mapSkills(ydoc, titleOf);
      else rowsOf.socialLinks = mapSocialLinks(ydoc, titleOf);
    } catch (error) {
      throw new Error(`${kind} page "${meta.title}" (${pageId}): ${error.message}`);
    } finally {
      ydoc.destroy();
    }
    const pageUpdated = updated([pageId, propertiesId, definitionsId]);
    documents.push({
      id: pageId, updatedAtUtc: pageUpdated, metadata: [], bookmarks: [], ...rowsOf,
      contentHash: contentHash({ kind, ...rowsOf }),
    });
  }
  process.stdout.write(JSON.stringify({
    capturedAtUtc: updated([...ids, ...pageIds.filter(pageId => records(pageId).length)]),
    documents,
  }));
} catch (error) {
  console.error(`AFFiNE export failed: ${error.message}`);
  process.exitCode = 1;
} finally {
  await client.end();
}
