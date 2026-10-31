using System.Data;
using System.Security.Cryptography;
using System.Text;
using Farm.Auth;
using Npgsql;

namespace Farm.Mind.Notes;

public class NoteDatabase(IConfiguration config, FarmAuthOptions auth)
{
    public async Task<NoteSession> OpenAsync(string subject, CancellationToken ct)
    {
        var connectionString = config["AffineNotes:ConnectionString"];
        var oidcClient = config["AffineNotes:OidcClientId"];
        var githubClient = config["AffineNotes:GitHubClientId"];
        if (string.IsNullOrWhiteSpace(connectionString) || (string.IsNullOrWhiteSpace(oidcClient) && string.IsNullOrWhiteSpace(githubClient)) ||
            (!string.IsNullOrWhiteSpace(oidcClient) && string.IsNullOrWhiteSpace(auth.Authority)))
            throw new NotePortalException("Configure the read-only AFFiNE database and its OIDC or GitHub client id on the Mind server.");

        var connection = new NpgsqlConnection(connectionString);
        try
        {
            await connection.OpenAsync(ct);
            var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
            await using (var readOnly = new NpgsqlCommand("SET TRANSACTION READ ONLY; SET LOCAL statement_timeout = '15000';", connection, transaction))
                await readOnly.ExecuteNonQueryAsync(ct);
            await using var identity = new NpgsqlCommand("""
                SELECT DISTINCT account.user_id
                FROM public.user_connected_accounts account
                JOIN public.users u ON u.id = account.user_id
                WHERE NOT u.disabled AND u.registered AND (
                  (account.provider = 'oidc' AND account.provider_namespace = @oidc_namespace AND account.provider_account_id = @subject)
                  OR (account.provider = 'github' AND @github_namespace <> '' AND
                      (account.provider_namespace = @github_namespace OR account.provider_namespace IS NULL) AND
                      account.provider_account_id = @github_id))
                LIMIT 2
                """, connection, transaction);
            identity.Parameters.AddWithValue("subject", subject);
            identity.Parameters.AddWithValue("oidc_namespace", string.IsNullOrWhiteSpace(oidcClient) ? "" : Namespace("oidc", auth.NormalizedAuthority, oidcClient));
            identity.Parameters.AddWithValue("github_namespace", string.IsNullOrWhiteSpace(githubClient) ? "" : Namespace("github", "https://github.com", githubClient));
            identity.Parameters.AddWithValue("github_id", subject.StartsWith("github:", StringComparison.Ordinal) ? subject[7..] : "");
            var users = new List<string>();
            await using (var reader = await identity.ExecuteReaderAsync(ct))
                while (await reader.ReadAsync(ct)) users.Add(reader.GetString(0));
            if (users.Count != 1)
                throw new NotePortalException("Sign in to Note with the same identity first. Missing, disabled or ambiguous Note identities cannot be read.");
            return new NoteSession(connection, transaction, users[0]);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    // AFFiNE namespaces accounts by SHA-256(issuer + newline + its OAuth client id), first 16 bytes.
    public static string Namespace(string provider, string issuer, string clientId) =>
        $"oauth:{provider}:{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{issuer.TrimEnd('/')}\n{clientId}")))[..32]}";
}

public sealed class NoteSession(NpgsqlConnection connection, NpgsqlTransaction transaction, string userId) : IAsyncDisposable
{
    private const string ActiveMember = """
        JOIN public.workspace_members member ON member.workspace_id = workspace.id AND member.user_id = @user
          AND member.state = 'active' AND member.role IN ('member', 'admin', 'owner')
        """;

    public async Task<IReadOnlyList<NoteWorkspaceDto>> ListWorkspacesAsync(CancellationToken ct)
    {
        await using var command = Command($"""
            SELECT workspace.id, coalesce(workspace.name, workspace.id)
            FROM public.workspaces workspace {ActiveMember} ORDER BY workspace.id
            """);
        var result = new List<NoteWorkspaceDto>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) result.Add(new(reader.GetString(0), reader.GetString(1)));
        return result;
    }

    public async Task AssertWorkspaceAsync(string workspaceId, CancellationToken ct)
    {
        await using var command = Command($"""
            SELECT EXISTS(SELECT 1 FROM public.workspaces workspace {ActiveMember} WHERE workspace.id = @workspace)
            """, workspaceId);
        if (await command.ExecuteScalarAsync(ct) is not true)
            throw new NotePortalException("Note workspace was not found or has no active membership for this account.");
    }

    public async Task<HashSet<string>> ReadableIdsAsync(string workspaceId, string[] ids, CancellationToken ct)
    {
        // Admin inheritance depends on a commercial license. Do not infer it from an unverified entitlement.
        // Active admins still have explicit/default read grants; owners always inherit document access.
        await using var command = Command($"""
            SELECT candidate.id
            FROM public.workspaces workspace {ActiveMember}
            CROSS JOIN unnest(@ids::text[]) candidate(id)
            LEFT JOIN public.workspace_access_policies policy ON policy.workspace_id = workspace.id
            LEFT JOIN public.doc_access_policies doc ON doc.workspace_id = workspace.id AND doc.doc_id = candidate.id
            LEFT JOIN public.doc_grants grant_ ON grant_.workspace_id = workspace.id AND grant_.doc_id = candidate.id
              AND grant_.principal_type = 'user' AND grant_.principal_id = @user
            LEFT JOIN public.workspace_pages page ON page.workspace_id = workspace.id AND page.page_id = candidate.id
            WHERE workspace.id = @workspace AND NOT coalesce(page.blocked, false)
              AND coalesce(doc.member_default_role, policy.member_default_doc_role, 'manager')
                IN ('none', 'external', 'reader', 'commenter', 'editor', 'manager', 'owner')
              AND (grant_.role IS NULL OR grant_.role IN ('none', 'external', 'reader', 'commenter', 'editor', 'manager', 'owner'))
              AND (member.role = 'owner'
                OR grant_.role IN ('external', 'reader', 'commenter', 'editor', 'manager', 'owner')
                OR ((grant_.role IS NULL OR grant_.role = 'none')
                  AND coalesce(doc.member_default_role, policy.member_default_doc_role, 'manager')
                    IN ('external', 'reader', 'commenter', 'editor', 'manager', 'owner')))
            """, workspaceId);
        command.Parameters.AddWithValue("ids", ids);
        var result = new HashSet<string>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) result.Add(reader.GetString(0));
        return result;
    }

    public async Task<IReadOnlyList<byte[]>> LoadAsync(string workspaceId, string docId, CancellationToken ct)
    {
        const int maxBytes = 16 * 1024 * 1024;
        await using var command = Command("""
            SELECT octet_length(blob), CASE WHEN octet_length(blob) <= 16777216 THEN blob ELSE NULL END, kind
            FROM (
              SELECT blob, updated_at AS timestamp, 0 AS kind FROM public.snapshots WHERE workspace_id = @workspace AND guid = @doc
              UNION ALL
              SELECT blob, created_at AS timestamp, 1 AS kind FROM public.updates WHERE workspace_id = @workspace AND guid = @doc
            ) data ORDER BY kind, timestamp LIMIT 10002
            """, workspaceId);
        command.Parameters.AddWithValue("doc", docId);
        var result = new List<byte[]>();
        long size = 0;
        var updateCount = 0;
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            size += reader.GetInt32(0);
            if (reader.GetInt32(2) == 1) updateCount++;
            if (size > maxBytes || updateCount > 10_000)
                throw new NotePortalException("Note exceeds the database reader's 16 MiB or 10000-update limit.");
            result.Add(reader.GetFieldValue<byte[]>(1));
        }
        if (result.Count == 0) throw new NotePortalException("Note document data was not found.");
        return result;
    }

    private NpgsqlCommand Command(string sql, string? workspaceId = null)
    {
        var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("user", userId);
        if (workspaceId is not null) command.Parameters.AddWithValue("workspace", workspaceId);
        return command;
    }

    public async ValueTask DisposeAsync()
    {
        await transaction.DisposeAsync();
        await connection.DisposeAsync();
    }
}
