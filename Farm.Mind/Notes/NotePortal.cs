using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Farm.Mind.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Farm.Mind.Notes;

// Only AFFiNE's permission-checked read tools are reachable, never arbitrary RPC methods or URLs.
public class NotePortal(MindDbContext db, IDataProtectionProvider protection, HttpClient client)
{
    public const string Origin = "https://note.eldervibe.dev";

    public async Task<IReadOnlyList<NoteConnectionDto>> ListAsync(string subject, CancellationToken ct) =>
        await db.NoteConnections.AsNoTracking().Where(c => c.Subject == subject).OrderBy(c => c.Name)
            .Select(c => new NoteConnectionDto(c.WorkspaceId, c.Name)).ToListAsync(ct);

    public async Task<NoteConnectionDto> ConnectAsync(string subject, ConnectNoteRequest request, CancellationToken ct)
    {
        ValidateId(request.WorkspaceId, "Workspace id");
        var name = request.Name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 200)
            throw new ArgumentException("A workspace name of 1-200 characters is required.");
        if (string.IsNullOrEmpty(request.Token) || request.Token.Length > 512 ||
            !request.Token.StartsWith("aff_mcp_v1.", StringComparison.Ordinal) || request.Token.Any(char.IsWhiteSpace))
            throw new ArgumentException("An AFFiNE workspace MCP credential is required.");

        var result = await RpcAsync(request.WorkspaceId, request.Token, "tools/list", new { }, ct);
        if (!result.TryGetProperty("tools", out var tools) || tools.ValueKind != JsonValueKind.Array ||
            !tools.EnumerateArray().Any(t => t.GetProperty("name").GetString() == "read_document") ||
            !tools.EnumerateArray().Any(t => t.GetProperty("name").GetString() == "doc_search"))
            throw new NotePortalException("This Note workspace does not support the required read tools.");
        if (tools.EnumerateArray().Any(t => t.GetProperty("name").GetString() is not ("read_document" or "doc_search")))
            throw new NotePortalException("Create a read-only Note credential before connecting this workspace.");

        var connection = await db.NoteConnections.FindAsync([subject, request.WorkspaceId], ct);
        if (connection is null)
        {
            connection = new NoteConnection { Subject = subject, WorkspaceId = request.WorkspaceId };
            db.NoteConnections.Add(connection);
        }
        connection.Name = name;
        connection.ProtectedToken = Protector(subject, request.WorkspaceId).Protect(request.Token);
        await db.SaveChangesAsync(ct);
        return new NoteConnectionDto(connection.WorkspaceId, connection.Name);
    }

    public async Task<bool> DisconnectAsync(string subject, string workspaceId, CancellationToken ct)
    {
        var connection = await db.NoteConnections.FindAsync([subject, workspaceId], ct);
        if (connection is null) return false;
        db.NoteConnections.Remove(connection);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<string> SearchAsync(string subject, string workspaceId, string query, int limit, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length > 2000)
            throw new ArgumentException("Search query must contain 1-2000 characters.");
        if (limit is < 1 or > 20) throw new ArgumentException("Search limit must be 1-20.");
        return await ReadToolAsync(subject, workspaceId, "doc_search", new { query, limit }, ct);
    }

    public async Task<NoteDocumentDto> ReadAsync(string subject, string workspaceId, string docId, int offset, int length, CancellationToken ct)
    {
        ValidateId(docId, "Document id");
        if (offset < 0 || length is < 1 or > 100_000)
            throw new ArgumentException("Offset must be nonnegative and length must be 1-100000.");
        var markdown = await ReadToolAsync(subject, workspaceId, "read_document", new { docId }, ct);
        var start = Math.Min(offset, markdown.Length);
        var count = Math.Min(length, markdown.Length - start);
        return new NoteDocumentDto(workspaceId, docId, markdown.Substring(start, count),
            start + count < markdown.Length ? start + count : null);
    }

    private async Task<string> ReadToolAsync(string subject, string workspaceId, string tool, object arguments, CancellationToken ct)
    {
        ValidateId(workspaceId, "Workspace id");
        var connection = await db.NoteConnections.AsNoTracking()
            .SingleOrDefaultAsync(c => c.Subject == subject && c.WorkspaceId == workspaceId, ct)
            ?? throw new NotePortalException("Connect this Note workspace in your signed-in account first.");
        string token;
        try { token = Protector(subject, workspaceId).Unprotect(connection.ProtectedToken); }
        catch (CryptographicException) { throw new NotePortalException("Reconnect this Note workspace; its credential cannot be decrypted."); }
        var result = await RpcAsync(workspaceId, token, "tools/call", new { name = tool, arguments }, ct);
        if (result.TryGetProperty("isError", out var error) && error.GetBoolean())
            throw new NotePortalException("Note could not read this document or search. Check your permissions and document id.");
        if (!result.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
            throw new NotePortalException("Note returned an invalid read response.");
        return string.Join("\n", content.EnumerateArray()
            .Where(c => c.TryGetProperty("type", out var type) && type.GetString() == "text")
            .Select(c => c.GetProperty("text").GetString()));
    }

    private async Task<JsonElement> RpcAsync(string workspaceId, string token, string method, object parameters, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"{Origin}/api/workspaces/{Uri.EscapeDataString(workspaceId)}/mcp")
        {
            Content = JsonContent.Create(new { jsonrpc = "2.0", id = 1, method, @params = parameters })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.ParseAdd("application/json");
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            await response.Content.LoadIntoBufferAsync(8 * 1024 * 1024, timeout.Token);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.Forbidden &&
                    json.RootElement.TryGetProperty("message", out var message) &&
                    message.ValueKind == JsonValueKind.String && message.GetString() == "Copilot is disabled.")
                    throw new NotePortalException("Note's workspace MCP endpoint is disabled. Enable copilot.enabled on the Note server before connecting.");
                throw new NotePortalException("Note access failed. The credential may be expired, revoked, or lack workspace access.");
            }
            if (json.RootElement.TryGetProperty("error", out _) ||
                !json.RootElement.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Object)
                throw new NotePortalException("Note rejected the read request. Check workspace permissions and server compatibility.");
            return result.Clone();
        }
        catch (HttpRequestException) { throw new NotePortalException("Note is unavailable or its response exceeds the read limit."); }
        catch (JsonException) { throw new NotePortalException("Note returned an invalid response."); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new NotePortalException("Note timed out. Retry the read request."); }
    }

    private IDataProtector Protector(string subject, string workspaceId) =>
        protection.CreateProtector("Farm.Mind.NoteCredential.v1", subject, workspaceId);

    private static void ValidateId(string? id, string label)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 128 || id is "." or ".." ||
            id.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('-' or '_')))
            throw new ArgumentException($"{label} must contain 1-128 letters, digits, hyphens or underscores.");
    }
}
