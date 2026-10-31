using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using Farm.Auth;
using Farm.Mind.Mcp;
using Farm.Mind.Notes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.FileProviders;
using Npgsql;
using Xunit;

namespace Farm.Mind.Tests;

public class NoteDatabaseFactAttribute : FactAttribute
{
    public NoteDatabaseFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MIND_NOTE_TEST_CONNECTION")))
            Skip = "Set MIND_NOTE_TEST_CONNECTION to an isolated PostgreSQL administrator connection to run database integration tests.";
    }
}

public class NoteDatabaseTests(NoteDatabaseFixture fixture) : IClassFixture<NoteDatabaseFixture>
{
    [NoteDatabaseFact]
    public async Task Identity_ExactProviderNamespace_DisabledAndAmbiguousAccountsFailClosedAsync()
    {
        var portal = fixture.Portal();
        Assert.Single(await portal.ListAsync("github:1", default));
        Assert.Empty(await portal.ListAsync("github:2", default));
        await Assert.ThrowsAsync<NotePortalException>(() => portal.ListAsync("github:unknown", default));
        await Assert.ThrowsAsync<NotePortalException>(() => portal.ListAsync("github:disabled", default));
        await Assert.ThrowsAsync<NotePortalException>(() => portal.ListAsync("github:wrong-issuer", default));
        await Assert.ThrowsAsync<NotePortalException>(() => fixture.Portal(github: true).ListAsync("github:ambiguous", default));
        Assert.Single(await fixture.Portal(github: true).ListAsync("github:legacy", default));
    }

    [NoteDatabaseFact]
    public async Task Search_FiltersCurrentGrantsTrashAndBlockedNotes_WithPaginationAsync()
    {
        var portal = fixture.Portal();
        var first = await portal.SearchAsync("github:1", "workspace-1", "", 1, 0, default);
        Assert.Equal("doc-1", Assert.Single(first.Notes).DocId);
        Assert.Equal(1, first.NextOffset);
        var second = await portal.SearchAsync("github:1", "workspace-1", "", 1, first.NextOffset!.Value, default);
        Assert.Equal("doc-2", Assert.Single(second.Notes).DocId);
        Assert.Null(second.NextOffset);
        Assert.Equal("doc-2", Assert.Single((await portal.SearchAsync("github:1", "workspace-1", "bEtA", 10, 0, default)).Notes).DocId);
        await Assert.ThrowsAsync<NotePortalException>(() => portal.SearchAsync("github:2", "workspace-1", "", 10, 0, default));
        await Assert.ThrowsAsync<NotePortalException>(() => portal.ReadAsync("github:1", "workspace-1", "doc-3", 0, 20, default));
        await Assert.ThrowsAsync<NotePortalException>(() => portal.ReadAsync("github:1", "workspace-1", "trash", 0, 20, default));
        await Assert.ThrowsAsync<NotePortalException>(() => portal.ReadAsync("github:1", "workspace-1", "blocked", 0, 20, default));
    }

    [NoteDatabaseFact]
    public async Task DocumentPolicy_ExplicitNoneFallsBack_OwnerInherits_AdminCannotBypassPolicyAsync()
    {
        var owner = await fixture.Portal(github: true).SearchAsync("github:legacy", "workspace-1", "", 20, 0, default);
        Assert.Contains(owner.Notes, p => p.DocId == "doc-3");
        var member = await fixture.Portal().SearchAsync("github:policy", "workspace-1", "", 20, 0, default);
        Assert.Contains(member.Notes, p => p.DocId == "doc-1");
        Assert.DoesNotContain(member.Notes, p => p.DocId == "doc-3");
        var admin = await fixture.Portal().SearchAsync("github:admin", "workspace-1", "", 20, 0, default);
        Assert.DoesNotContain(admin.Notes, p => p.DocId == "doc-3");
    }

    [Theory]
    [InlineData("../other")]
    [InlineData("https://other.test")]
    [InlineData("workspace/other")]
    [InlineData("..")]
    public async Task Read_InvalidIds_AreRejectedBeforeAnyDatabaseAccessAsync(string id) =>
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Portal().ReadAsync("github:1", id, "doc-1", 0, 20, default));

    [NoteDatabaseFact]
    public async Task Read_MergesPendingUpdates_PaginatesAndUsesTheMcpSubjectAsync()
    {
        var portal = fixture.Portal();
        var alice = new NoteTools(portal, new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "github:1")], "test"))
            }
        });
        var first = await alice.ReadNote("workspace-1", "doc-1", length: 10);
        var next = await alice.ReadNote("workspace-1", "doc-1", offset: first.NextOffset!.Value);
        Assert.Equal("# Alpha note\n\nLatest private text", first.Markdown + next.Markdown);
        Assert.Empty(next.UnsupportedBlocks);
        Assert.Null(next.NextOffset);
        var bob = new NoteTools(portal, new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "github:2")], "test"))
            }
        });
        await Assert.ThrowsAsync<ModelContextProtocol.McpException>(() => bob.ReadNote("workspace-1", "doc-1"));
    }

    [NoteDatabaseFact]
    public async Task DatabaseRole_CannotWrite_AndReadsNeverCompactSnapshotsAsync()
    {
        await using var connection = new NpgsqlConnection(fixture.ReaderConnection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("DELETE FROM public.snapshots", connection);
        await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        await fixture.Portal().ReadAsync("github:1", "workspace-1", "doc-1", 0, 20_000, default);
        command.CommandText = "SELECT count(*) FROM public.updates";
        Assert.Equal(1L, await command.ExecuteScalarAsync());
        command.CommandText = "SELECT encode(blob, 'base64') FROM public.snapshots WHERE guid='doc-1'";
        Assert.Equal(fixture.Snapshot, ((string)(await command.ExecuteScalarAsync())!).Replace("\n", ""));
        command.CommandText = "SELECT access_token FROM public.user_connected_accounts";
        await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
    }

    [NoteDatabaseFact]
    public async Task Read_RevokedMembershipIsDeniedOnTheNextRequestAsync()
    {
        var portal = fixture.Portal();
        Assert.Single(await portal.ListAsync("github:revoke", default));
        await fixture.ExecuteAsync("UPDATE workspace_members SET state='inactive' WHERE user_id='revocable'");
        Assert.Empty(await portal.ListAsync("github:revoke", default));
        await Assert.ThrowsAsync<NotePortalException>(() => portal.ReadAsync("github:revoke", "workspace-1", "doc-1", 0, 20, default));
    }
}

public class NoteDatabaseFixture : IAsyncLifetime
{
    private readonly string _database = "mind_note_test_" + Guid.NewGuid().ToString("N");
    private readonly string _role = "mind_note_reader_" + Guid.NewGuid().ToString("N");
    private string _admin = "";
    public string ReaderConnection { get; private set; } = "";
    public string Snapshot { get; private set; } = "";
    private static string Source => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Farm.Mind"));

    public NotePortal Portal(bool github = false)
    {
        var auth = new FarmAuthOptions { Authority = "https://identity.test" };
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AffineNotes:ConnectionString"] = ReaderConnection,
            ["AffineNotes:OidcClientId"] = "note",
            ["AffineNotes:GitHubClientId"] = github ? "note-github" : null
        }).Build();
        return new NotePortal(new NoteDatabase(config, auth), new NoteDecoder(new NoteTestEnvironment { ContentRootPath = Source }));
    }

    internal class NoteTestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "Farm.Mind";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    public async Task InitializeAsync()
    {
        var admin = Environment.GetEnvironmentVariable("MIND_NOTE_TEST_CONNECTION");
        if (string.IsNullOrEmpty(admin)) return;
        await using (var connection = new NpgsqlConnection(admin))
        {
            await connection.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE {_database}; CREATE ROLE {_role} LOGIN PASSWORD 'isolated-test-only';", connection);
            // CREATE DATABASE cannot be executed in an implicit multi-statement transaction.
            create.CommandText = $"CREATE DATABASE {_database}";
            await create.ExecuteNonQueryAsync();
            create.CommandText = $"CREATE ROLE {_role} LOGIN PASSWORD 'isolated-test-only'";
            await create.ExecuteNonQueryAsync();
        }
        _admin = new NpgsqlConnectionStringBuilder(admin) { Database = _database, Pooling = false }.ConnectionString;
        ReaderConnection = new NpgsqlConnectionStringBuilder(_admin) { Username = _role, Password = "isolated-test-only" }.ConnectionString;
        await ExecuteAsync("""
            CREATE TABLE users(id text PRIMARY KEY, disabled boolean NOT NULL DEFAULT false, registered boolean NOT NULL DEFAULT true);
            CREATE TABLE user_connected_accounts(user_id text, provider text, provider_namespace text, provider_account_id text, access_token text);
            CREATE TABLE workspaces(id text PRIMARY KEY, name text);
            CREATE TABLE workspace_members(workspace_id text, user_id text, state text, role text);
            CREATE TABLE workspace_access_policies(workspace_id text PRIMARY KEY, member_default_doc_role text);
            CREATE TABLE doc_access_policies(workspace_id text, doc_id text, member_default_role text);
            CREATE TABLE doc_grants(workspace_id text, doc_id text, principal_type text, principal_id text, role text);
            CREATE TABLE workspace_pages(workspace_id text, page_id text, blocked boolean);
            CREATE TABLE snapshots(workspace_id text, guid text, blob bytea, updated_at timestamptz);
            CREATE TABLE updates(workspace_id text, guid text, blob bytea, created_at timestamptz);
            INSERT INTO users(id) VALUES('alice'),('bob'),('wrong'),('ambiguous-a'),('ambiguous-b'),('legacy'),('revocable'),('policy'),('admin');
            INSERT INTO users(id,disabled) VALUES('disabled',true);
            INSERT INTO workspaces VALUES('workspace-1','My Notes');
            INSERT INTO workspace_members VALUES('workspace-1','alice','active','member'),('workspace-1','bob','pending','member'),
              ('workspace-1','legacy','active','owner'),('workspace-1','revocable','active','owner'),
              ('workspace-1','policy','active','member'),('workspace-1','admin','active','admin');
            INSERT INTO workspace_access_policies VALUES('workspace-1','reader');
            INSERT INTO doc_access_policies VALUES('workspace-1','doc-2','none'),('workspace-1','doc-3','none');
            INSERT INTO doc_grants VALUES('workspace-1','doc-2','user','alice','reader'),('workspace-1','doc-1','user','policy','none');
            INSERT INTO workspace_pages VALUES('workspace-1','blocked',true);
            """);
        await ExecuteAsync($"""
            GRANT CONNECT ON DATABASE {_database} TO {_role};
            GRANT USAGE ON SCHEMA public TO {_role};
            GRANT SELECT (id, disabled, registered) ON users TO {_role};
            GRANT SELECT (user_id, provider, provider_namespace, provider_account_id) ON user_connected_accounts TO {_role};
            GRANT SELECT (id, name) ON workspaces TO {_role};
            GRANT SELECT (workspace_id, user_id, role, state) ON workspace_members TO {_role};
            GRANT SELECT (workspace_id, member_default_doc_role) ON workspace_access_policies TO {_role};
            GRANT SELECT (workspace_id, doc_id, member_default_role) ON doc_access_policies TO {_role};
            GRANT SELECT (workspace_id, doc_id, principal_type, principal_id, role) ON doc_grants TO {_role};
            GRANT SELECT (workspace_id, page_id, blocked) ON workspace_pages TO {_role};
            GRANT SELECT (workspace_id, guid, blob, updated_at) ON snapshots TO {_role};
            GRANT SELECT (workspace_id, guid, blob, created_at) ON updates TO {_role};
            ALTER ROLE {_role} SET default_transaction_read_only=on;
            """);
        var ns = NoteDatabase.Namespace("oidc", "https://identity.test", "note");
        var githubNs = NoteDatabase.Namespace("github", "https://github.com", "note-github");
        await using (var connection = new NpgsqlConnection(_admin))
        {
            await connection.OpenAsync();
            foreach (var (user, provider, namespace_, subject) in new[]
            {
                ("alice", "oidc", ns, "github:1"), ("bob", "oidc", ns, "github:2"),
                ("wrong", "oidc", "other-issuer", "github:wrong-issuer"), ("disabled", "oidc", ns, "github:disabled"),
                ("ambiguous-a", "oidc", ns, "github:ambiguous"), ("ambiguous-b", "github", githubNs, "ambiguous"),
                ("legacy", "github", (string?)null, "legacy"), ("revocable", "oidc", ns, "github:revoke"),
                ("policy", "oidc", ns, "github:policy"), ("admin", "oidc", ns, "github:admin")
            })
            {
                await using var command = new NpgsqlCommand("INSERT INTO user_connected_accounts(user_id,provider,provider_namespace,provider_account_id) VALUES(@user,@provider,@namespace,@subject)", connection);
                command.Parameters.AddWithValue("user", user);
                command.Parameters.AddWithValue("provider", provider);
                command.Parameters.AddWithValue("namespace", NpgsqlTypes.NpgsqlDbType.Text, (object?)namespace_ ?? DBNull.Value);
                command.Parameters.AddWithValue("subject", subject);
                await command.ExecuteNonQueryAsync();
            }
            using var process = Process.Start(new ProcessStartInfo("node", Path.Combine(Source, "Notes/reader/fixtures.mjs"))
            {
                RedirectStandardOutput = true,
                UseShellExecute = false
            })!;
            var json = JsonDocument.Parse(await process.StandardOutput.ReadToEndAsync());
            await process.WaitForExitAsync();
            if (process.ExitCode != 0) throw new InvalidOperationException("Note fixtures require npm ci in Farm.Mind/Notes/reader.");
            Snapshot = json.RootElement.GetProperty("snapshot").GetString()!;
            foreach (var (id, property, table, timestamp) in new[]
            {
                ("workspace-1", "root", "snapshots", "updated_at"), ("doc-1", "snapshot", "snapshots", "updated_at"),
                ("doc-1", "update", "updates", "created_at")
            })
            {
                await using var command = new NpgsqlCommand($"INSERT INTO {table}(workspace_id,guid,blob,{timestamp}) VALUES('workspace-1',@id,@blob,now())", connection);
                command.Parameters.AddWithValue("id", id);
                command.Parameters.AddWithValue("blob", Convert.FromBase64String(json.RootElement.GetProperty(property).GetString()!));
                await command.ExecuteNonQueryAsync();
            }
        }
    }

    public async Task ExecuteAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_admin);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        if (string.IsNullOrEmpty(_admin)) return;
        await using var connection = new NpgsqlConnection(Environment.GetEnvironmentVariable("MIND_NOTE_TEST_CONNECTION"));
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE {_database}; DROP ROLE {_role}", connection);
        command.CommandText = $"DROP DATABASE {_database}";
        await command.ExecuteNonQueryAsync();
        command.CommandText = $"DROP ROLE {_role}";
        await command.ExecuteNonQueryAsync();
    }
}
