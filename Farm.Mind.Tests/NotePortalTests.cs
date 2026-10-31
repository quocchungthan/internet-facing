using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Farm.Mind.Data;
using Farm.Mind.Notes;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Farm.Mind.Tests;

public class NotePortalTests
{
    private const string Token = "aff_mcp_v1.test.read-only";

    private static MindDbContext Database() => new(new DbContextOptionsBuilder<MindDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public async Task Connect_ValidCredential_EncryptsAndScopesDelegationAsync()
    {
        using var db = Database();
        var handler = new NoteHandler();
        var portal = new NotePortal(db, new EphemeralDataProtectionProvider(), new HttpClient(handler));
        await portal.ConnectAsync("alice", new("workspace-1", "Notes", Token), default);

        var stored = Assert.Single(db.NoteConnections);
        Assert.DoesNotContain(Token, stored.ProtectedToken);
        Assert.DoesNotContain(Token, JsonSerializer.Serialize(await portal.ListAsync("alice", default)));
        Assert.Empty(await portal.ListAsync("bob", default));
        Assert.False(await portal.DisconnectAsync("bob", "workspace-1", default));

        handler.ReadText = "Private Markdown";
        var found = await portal.ReadAsync("alice", "workspace-1", "doc-1", 0, 20_000, default);
        Assert.Equal("Private Markdown", found.Markdown);
        Assert.Equal(Token, handler.LastToken);
        var calls = handler.Calls;
        await Assert.ThrowsAsync<NotePortalException>(() => portal.ReadAsync("bob", "workspace-1", "doc-1", 0, 20_000, default));
        Assert.Equal(calls, handler.Calls);
        Assert.True(await portal.DisconnectAsync("alice", "workspace-1", default));
        await Assert.ThrowsAsync<NotePortalException>(() => portal.SearchAsync("alice", "workspace-1", "text", 10, default));
    }

    [Fact]
    public async Task Read_LargeDocument_ReturnsContinuationAndOnlyReadCallsAsync()
    {
        using var db = Database();
        var handler = new NoteHandler { ReadText = "abcdef" };
        var portal = new NotePortal(db, new EphemeralDataProtectionProvider(), new HttpClient(handler));
        await portal.ConnectAsync("alice", new("workspace-1", "Notes", Token), default);

        var first = await portal.ReadAsync("alice", "workspace-1", "doc-1", 0, 3, default);
        Assert.Equal("abc", first.Markdown);
        Assert.Equal(3, first.NextOffset);
        var last = await portal.ReadAsync("alice", "workspace-1", "doc-1", first.NextOffset!.Value, 3, default);
        Assert.Equal("def", last.Markdown);
        Assert.Null(last.NextOffset);
        Assert.Empty((await portal.ReadAsync("alice", "workspace-1", "doc-1", 100, 3, default)).Markdown);
        await portal.SearchAsync("alice", "workspace-1", "keyword", 7, default);

        Assert.Equal("doc_search", handler.LastRequest.GetProperty("params").GetProperty("name").GetString());
        Assert.Equal(7, handler.LastRequest.GetProperty("params").GetProperty("arguments").GetProperty("limit").GetInt32());
        Assert.All(handler.ToolNames, name => Assert.Contains(name, new[] { "read_document", "doc_search" }));
        Assert.Equal($"{NotePortal.Origin}/api/workspaces/workspace-1/mcp", handler.LastUri!.ToString());
    }

    [Fact]
    public async Task Connect_WriteToolsOrInvalidCredential_DoesNotPersistAsync()
    {
        using var db = Database();
        var handler = new NoteHandler { ExposeWrites = true };
        var portal = new NotePortal(db, new EphemeralDataProtectionProvider(), new HttpClient(handler));
        await Assert.ThrowsAsync<NotePortalException>(() => portal.ConnectAsync("alice", new("workspace-1", "Notes", Token), default));
        handler.ExposeWrites = false;
        handler.Status = HttpStatusCode.Unauthorized;
        await Assert.ThrowsAsync<NotePortalException>(() => portal.ConnectAsync("alice", new("workspace-1", "Notes", Token), default));
        Assert.Empty(db.NoteConnections);
    }

    [Theory]
    [InlineData("../other")]
    [InlineData("https://other.test")]
    [InlineData("workspace/other")]
    [InlineData("..")]
    public async Task Connect_InvalidWorkspaceId_DoesNotSendCredentialAsync(string id)
    {
        using var db = Database();
        var handler = new NoteHandler();
        var portal = new NotePortal(db, new EphemeralDataProtectionProvider(), new HttpClient(handler));
        await Assert.ThrowsAsync<ArgumentException>(() => portal.ConnectAsync("alice", new(id, "Notes", Token), default));
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Read_RevokedAccessOrUpstreamError_DoesNotLeakErrorDetailsAsync()
    {
        using var db = Database();
        var handler = new NoteHandler();
        var portal = new NotePortal(db, new EphemeralDataProtectionProvider(), new HttpClient(handler));
        await portal.ConnectAsync("alice", new("workspace-1", "Notes", Token), default);

        handler.Status = HttpStatusCode.Forbidden;
        await Assert.ThrowsAsync<NotePortalException>(() => portal.ReadAsync("alice", "workspace-1", "doc-1", 0, 3, default));
        handler.Status = HttpStatusCode.OK;
        handler.ToolError = true;
        var error = await Assert.ThrowsAsync<NotePortalException>(() => portal.ReadAsync("alice", "workspace-1", "doc-1", 0, 3, default));
        Assert.DoesNotContain("upstream-sensitive-detail", error.Message);
    }

    [Fact]
    public async Task Connect_CopilotDisabled_ExplainsServerPrerequisiteAsync()
    {
        using var db = Database();
        var handler = new NoteHandler { Status = HttpStatusCode.Forbidden, CopilotDisabled = true };
        var portal = new NotePortal(db, new EphemeralDataProtectionProvider(), new HttpClient(handler));
        var error = await Assert.ThrowsAsync<NotePortalException>(() => portal.ConnectAsync("alice", new("workspace-1", "Notes", Token), default));
        Assert.Contains("copilot.enabled", error.Message);
        Assert.Empty(db.NoteConnections);
    }

    [Fact]
    public async Task Read_CredentialCopiedBetweenSubjects_CannotDecryptAsync()
    {
        using var db = Database();
        var handler = new NoteHandler();
        var portal = new NotePortal(db, new EphemeralDataProtectionProvider(), new HttpClient(handler));
        await portal.ConnectAsync("alice", new("workspace-1", "Notes", Token), default);
        db.NoteConnections.Add(new NoteConnection
        {
            Subject = "bob",
            WorkspaceId = "workspace-1",
            Name = "Copied",
            ProtectedToken = Assert.Single(db.NoteConnections).ProtectedToken
        });
        await db.SaveChangesAsync();

        var calls = handler.Calls;
        await Assert.ThrowsAsync<NotePortalException>(() => portal.ReadAsync("bob", "workspace-1", "doc-1", 0, 3, default));
        Assert.Equal(calls, handler.Calls);
    }

    [Fact]
    public async Task Search_InvalidBounds_DoesNotContactNoteAsync()
    {
        using var db = Database();
        var handler = new NoteHandler();
        var portal = new NotePortal(db, new EphemeralDataProtectionProvider(), new HttpClient(handler));
        await Assert.ThrowsAsync<ArgumentException>(() => portal.SearchAsync("alice", "workspace-1", "", 10, default));
        await Assert.ThrowsAsync<ArgumentException>(() => portal.SearchAsync("alice", "workspace-1", "query", 21, default));
        await Assert.ThrowsAsync<ArgumentException>(() => portal.ReadAsync("alice", "workspace-1", "doc-1", -1, 3, default));
        await Assert.ThrowsAsync<ArgumentException>(() => portal.ReadAsync("alice", "workspace-1", "doc-1", 0, 100_001, default));
        Assert.Equal(0, handler.Calls);
    }
}

internal class NoteHandler : HttpMessageHandler
{
    public string ReadText { get; set; } = "Markdown from Note";
    public bool ExposeWrites { get; set; }
    public bool ToolError { get; set; }
    public bool CopilotDisabled { get; set; }
    public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
    public int Calls { get; private set; }
    public string? LastToken { get; private set; }
    public Uri? LastUri { get; private set; }
    public JsonElement LastRequest { get; private set; }
    public List<string> ToolNames { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Calls++;
        LastUri = request.RequestUri;
        LastToken = request.Headers.Authorization?.Parameter;
        LastRequest = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct)).RootElement.Clone();
        object result;
        if (LastRequest.GetProperty("method").GetString() == "tools/list")
        {
            var names = new List<string> { "read_document", "doc_search" };
            if (ExposeWrites) names.Add("create_document");
            result = new { tools = names.Select(name => new { name }).ToArray() };
        }
        else
        {
            var name = LastRequest.GetProperty("params").GetProperty("name").GetString()!;
            ToolNames.Add(name);
            result = new
            {
                isError = ToolError,
                content = new[] { new { type = "text", text = ToolError ? "upstream-sensitive-detail" : ReadText } }
            };
        }
        return new HttpResponseMessage(Status)
        {
            Content = CopilotDisabled
                ? JsonContent.Create(new { code = "ACTION_FORBIDDEN", message = "Copilot is disabled." })
                : JsonContent.Create(new { jsonrpc = "2.0", id = 1, result })
        };
    }
}
