using System.ComponentModel;
using System.Security.Claims;
using Farm.Mind.Data;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Farm.Mind.Mcp;

[McpServerToolType]
public class MindTools(MemoryStore store, IHttpContextAccessor http)
{
    private string Subject => http.HttpContext?.User.FindFirstValue("sub")
        ?? throw new InvalidOperationException("No authenticated subject.");

    [McpServerTool(Name = "get_briefing", ReadOnly = true), Description("Start here. Returns the most recent memories and the linked workspaces of the signed-in user.")]
    public Task<Briefing> GetBriefing(CancellationToken ct) => store.BriefingAsync(Subject, ct);

    [McpServerTool(Name = "search", ReadOnly = true), Description("Full-text search over the user's memories. An empty query returns the most recently updated ones.")]
    public Task<IReadOnlyList<MemoryDto>> Search(
        [Description("Search terms; supports quoted phrases, OR and -exclusions.")] string query,
        [Description("Optional workspace name to restrict the search.")] string? workspace = null,
        [Description("Maximum results, 1-25.")] int limit = 10,
        CancellationToken ct = default) => store.SearchAsync(Subject, query, workspace, limit, ct);

    [McpServerTool(Name = "get_memory", ReadOnly = true), Description("Fetch one memory by id.")]
    public async Task<MemoryDto> GetMemory([Description("Memory id.")] Guid id, CancellationToken ct) =>
        await store.GetAsync(Subject, id, ct) ?? throw new McpException($"Memory {id} was not found.");

    [McpServerTool(Name = "save_memory"), Description("Save a new memory for the signed-in user.")]
    public Task<MemoryDto> SaveMemory(
        [Description("Short title.")] string title,
        [Description("Memory content (markdown allowed).")] string content,
        [Description("Optional tags.")] string[]? tags = null,
        [Description("Optional workspace name this memory belongs to.")] string? workspace = null,
        CancellationToken ct = default) => Guard(() => store.SaveAsync(Subject, title, content, tags, workspace, ct));

    [McpServerTool(Name = "update_memory", Idempotent = true), Description("Update fields of an existing memory; omitted fields stay unchanged.")]
    public async Task<MemoryDto> UpdateMemory(
        [Description("Memory id.")] Guid id,
        string? title = null,
        string? content = null,
        string[]? tags = null,
        string? workspace = null,
        CancellationToken ct = default) =>
        await Guard(() => store.UpdateAsync(Subject, id, title, content, tags, workspace, ct)) ?? throw new McpException($"Memory {id} was not found.");

    [McpServerTool(Name = "forget_memory", Destructive = true, Idempotent = true), Description("Permanently delete a memory.")]
    public async Task<string> ForgetMemory([Description("Memory id.")] Guid id, CancellationToken ct) =>
        await store.ForgetAsync(Subject, id, ct) ? "Forgotten." : throw new McpException($"Memory {id} was not found.");

    [McpServerTool(Name = "list_workspaces", ReadOnly = true), Description("List the workspaces the user has linked.")]
    public Task<IReadOnlyList<WorkspaceDto>> ListWorkspaces(CancellationToken ct) => store.ListWorkspacesAsync(Subject, ct);

    [McpServerTool(Name = "link_workspace", Idempotent = true), Description("Link (or update) a named workspace, optionally with its AFFiNE workspace id.")]
    public Task<WorkspaceDto> LinkWorkspace(
        [Description("Workspace name.")] string name,
        [Description("Optional AFFiNE workspace id.")] string? affineWorkspaceId = null,
        CancellationToken ct = default) => Guard(() => store.LinkWorkspaceAsync(Subject, name, affineWorkspaceId, ct));

    // Validation messages are safe to show to the model; anything else stays a generic error.
    private static async Task<T> Guard<T>(Func<Task<T>> action)
    {
        try { return await action(); }
        catch (ArgumentException e) { throw new McpException(e.Message); }
    }
}
