using Microsoft.EntityFrameworkCore;

namespace Farm.Mind.Data;

public record MemoryDto(Guid Id, string Title, string Content, string[] Tags, string? Workspace, DateTimeOffset UpdatedAt);

public record WorkspaceDto(string Name, string? AffineWorkspaceId);

public record Briefing(string Guidance, IReadOnlyList<MemoryDto> Recent, IReadOnlyList<WorkspaceDto> Workspaces);

// Every operation is scoped to the Fences subject, so one user can never read or change another user's memories.
public class MemoryStore(MindDbContext db)
{
    public const int MaxContentLength = 20_000;
    public const int MaxSearchResults = 25;

    public async Task<MemoryDto> SaveAsync(string subject, string title, string content, IEnumerable<string>? tags, string? workspace, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var memory = new Memory
        {
            Id = Guid.NewGuid(),
            Subject = subject,
            CreatedAt = now,
            UpdatedAt = now
        };
        Apply(memory, title, content, tags, workspace);
        db.Memories.Add(memory);
        await db.SaveChangesAsync(ct);
        return ToDto(memory);
    }

    public async Task<MemoryDto?> UpdateAsync(string subject, Guid id, string? title, string? content, IEnumerable<string>? tags, string? workspace, CancellationToken ct = default)
    {
        var memory = await db.Memories.FirstOrDefaultAsync(m => m.Id == id && m.Subject == subject, ct);
        if (memory is null) return null;

        Apply(memory, title ?? memory.Title, content ?? memory.Content, tags ?? Split(memory.Tags), workspace ?? memory.Workspace);
        memory.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToDto(memory);
    }

    public async Task<MemoryDto?> GetAsync(string subject, Guid id, CancellationToken ct = default)
    {
        var memory = await db.Memories.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id && m.Subject == subject, ct);
        return memory is null ? null : ToDto(memory);
    }

    public async Task<bool> ForgetAsync(string subject, Guid id, CancellationToken ct = default)
    {
        var memory = await db.Memories.FirstOrDefaultAsync(m => m.Id == id && m.Subject == subject, ct);
        if (memory is null) return false;
        db.Memories.Remove(memory);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<MemoryDto>> SearchAsync(string subject, string query, string? workspace = null, int limit = 10, CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, MaxSearchResults);
        var memories = db.Memories.AsNoTracking().Where(m => m.Subject == subject);
        if (!string.IsNullOrWhiteSpace(workspace))
            memories = memories.Where(m => m.Workspace == workspace);

        List<Memory> found;
        if (string.IsNullOrWhiteSpace(query))
        {
            found = await memories.OrderByDescending(m => m.UpdatedAt).Take(limit).ToListAsync(ct);
        }
        else if (db.Database.IsNpgsql())
        {
            found = await memories
                .Where(m => m.SearchVector!.Matches(EF.Functions.WebSearchToTsQuery("english", query)))
                .OrderByDescending(m => m.SearchVector!.Rank(EF.Functions.WebSearchToTsQuery("english", query)))
                .Take(limit)
                .ToListAsync(ct);
        }
        else
        {
            var needle = query.Trim().ToLowerInvariant();
            found = await memories
                .Where(m => m.Title.ToLower().Contains(needle) || m.Content.ToLower().Contains(needle) || m.Tags.Contains(needle))
                .OrderByDescending(m => m.UpdatedAt)
                .Take(limit)
                .ToListAsync(ct);
        }

        return found.Select(ToDto).ToList();
    }

    public async Task<Briefing> BriefingAsync(string subject, CancellationToken ct = default)
    {
        var recent = await db.Memories.AsNoTracking()
            .Where(m => m.Subject == subject)
            .OrderByDescending(m => m.UpdatedAt)
            .Take(10)
            .ToListAsync(ct);
        return new Briefing(Mcp.MindGuidance.Text, recent.Select(ToDto).ToList(), await ListWorkspacesAsync(subject, ct));
    }

    public async Task<IReadOnlyList<WorkspaceDto>> ListWorkspacesAsync(string subject, CancellationToken ct = default)
    {
        var workspaces = await db.Workspaces.AsNoTracking().Where(w => w.Subject == subject).OrderBy(w => w.Name).ToListAsync(ct);
        return workspaces.Select(w => new WorkspaceDto(w.Name, w.AffineWorkspaceId)).ToList();
    }

    public async Task<WorkspaceDto> LinkWorkspaceAsync(string subject, string name, string? affineWorkspaceId, CancellationToken ct = default)
    {
        name = name.Trim();
        if (name.Length is 0 or > 200) throw new ArgumentException("Workspace name must be 1-200 characters.");

        var workspace = await db.Workspaces.FirstOrDefaultAsync(w => w.Subject == subject && w.Name == name, ct);
        if (workspace is null)
        {
            workspace = new LinkedWorkspace { Id = Guid.NewGuid(), Subject = subject, Name = name, CreatedAt = DateTimeOffset.UtcNow };
            db.Workspaces.Add(workspace);
        }
        workspace.AffineWorkspaceId = string.IsNullOrWhiteSpace(affineWorkspaceId) ? workspace.AffineWorkspaceId : affineWorkspaceId.Trim();
        await db.SaveChangesAsync(ct);
        return new WorkspaceDto(workspace.Name, workspace.AffineWorkspaceId);
    }

    private static void Apply(Memory memory, string title, string content, IEnumerable<string>? tags, string? workspace)
    {
        title = title.Trim();
        if (title.Length is 0 or > 300) throw new ArgumentException("Title must be 1-300 characters.");
        if (string.IsNullOrWhiteSpace(content)) throw new ArgumentException("Content is required.");
        if (content.Length > MaxContentLength) throw new ArgumentException($"Content is limited to {MaxContentLength} characters.");

        memory.Title = title;
        memory.Content = content;
        memory.Tags = string.Join(' ', (tags ?? []).Select(t => t.Trim().ToLowerInvariant().Replace(' ', '-')).Where(t => t.Length > 0).Distinct().Take(20));
        memory.Workspace = string.IsNullOrWhiteSpace(workspace) ? null : workspace.Trim();
    }

    private static string[] Split(string tags) => tags.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    private static MemoryDto ToDto(Memory m) => new(m.Id, m.Title, m.Content, Split(m.Tags), m.Workspace, m.UpdatedAt);
}
