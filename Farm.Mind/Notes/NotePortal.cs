using Npgsql;

namespace Farm.Mind.Notes;

public record NoteWorkspaceDto(string WorkspaceId, string Name);
public record NoteSummaryDto(string DocId, string Title);
public record NoteSearchDto(IReadOnlyList<NoteSummaryDto> Notes, int? NextOffset);

public class NotePortal(NoteDatabase database, NoteDecoder decoder)
{
    public Task<IReadOnlyList<NoteWorkspaceDto>> ListAsync(string subject, CancellationToken ct) =>
        ReadAsync(subject, session => session.ListWorkspacesAsync(ct), ct);

    // Titles come from the live workspace root, not an AI index that may be disabled or stale.
    public Task<NoteSearchDto> SearchAsync(string subject, string workspaceId, string query, int limit, int offset, CancellationToken ct)
    {
        ValidateId(workspaceId, "Workspace id");
        if (query is null || query.Length > 2000) throw new ArgumentException("Search query must be at most 2000 characters.");
        if (limit is < 1 or > 20 || offset < 0) throw new ArgumentException("Limit must be 1-20 and offset must be nonnegative.");
        return ReadAsync(subject, async session =>
        {
            var pages = await RootAsync(session, workspaceId, ct);
            var readable = await session.ReadableIdsAsync(workspaceId, pages.Select(p => p.DocId).ToArray(), ct);
            var hits = pages.Where(p => readable.Contains(p.DocId) && p.Title.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p.DocId, StringComparer.Ordinal).Skip(offset).Take(limit + 1).ToArray();
            return new NoteSearchDto(hits.Take(limit).ToArray(), hits.Length > limit ? checked(offset + limit) : null);
        }, ct);
    }

    public Task<NoteDocumentDto> ReadAsync(string subject, string workspaceId, string docId, int offset, int length, CancellationToken ct)
    {
        ValidateId(workspaceId, "Workspace id");
        ValidateId(docId, "Document id");
        if (offset < 0 || length is < 1 or > 100_000)
            throw new ArgumentException("Offset must be nonnegative and length must be 1-100000.");
        return ReadAsync(subject, async session =>
        {
            var pages = await RootAsync(session, workspaceId, ct);
            if (!pages.Any(p => p.DocId == docId) ||
                !(await session.ReadableIdsAsync(workspaceId, [docId], ct)).Contains(docId))
                throw new NotePortalException("Note was not found or is not readable.");
            var updates = await session.LoadAsync(workspaceId, docId, ct);
            var decoded = await decoder.DecodeAsync(updates, false, ct);
            var start = Math.Min(offset, decoded.Markdown.Length);
            var count = Math.Min(length, decoded.Markdown.Length - start);
            return new NoteDocumentDto(workspaceId, docId, decoded.Markdown.Substring(start, count),
                start + count < decoded.Markdown.Length ? start + count : null, decoded.UnsupportedBlocks);
        }, ct);
    }

    private async Task<IReadOnlyList<NoteSummaryDto>> RootAsync(NoteSession session, string workspaceId, CancellationToken ct)
    {
        await session.AssertWorkspaceAsync(workspaceId, ct);
        var root = await decoder.DecodeAsync(await session.LoadAsync(workspaceId, workspaceId, ct), true, ct);
        return root.Pages.Where(p => p.DocId != workspaceId && IsId(p.DocId)).ToArray();
    }

    private async Task<T> ReadAsync<T>(string subject, Func<NoteSession, Task<T>> action, CancellationToken ct)
    {
        try
        {
            await using var session = await database.OpenAsync(subject, ct);
            return await action(session);
        }
        catch (NpgsqlException) { throw new NotePortalException("The read-only Note database is unavailable or its schema is incompatible."); }
        catch (ArgumentException) { throw new NotePortalException("The read-only Note database configuration is invalid."); }
    }

    private static bool IsId(string? id) => !string.IsNullOrWhiteSpace(id) && id.Length <= 128 && id is not ("." or "..") &&
        id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    private static void ValidateId(string? id, string label)
    {
        if (!IsId(id)) throw new ArgumentException($"{label} must contain 1-128 letters, digits, hyphens or underscores.");
    }
}
