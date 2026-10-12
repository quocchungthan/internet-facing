using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;

namespace Farm.Mind.Data;

public class Memory
{
    public Guid Id { get; set; }
    public string Subject { get; set; } = "";
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    // Space-separated, lowercase. Kept as one string so it can feed the full-text index.
    public string Tags { get; set; } = "";
    public string? Workspace { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public NpgsqlTsVector? SearchVector { get; set; }
}

public class LinkedWorkspace
{
    public Guid Id { get; set; }
    public string Subject { get; set; } = "";
    public string Name { get; set; } = "";
    public string? AffineWorkspaceId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class AccessKey
{
    public Guid Id { get; set; }
    public string Subject { get; set; } = "";
    public string Name { get; set; } = "";
    // Only the SHA-256 of the key is stored; the key itself is shown once at creation.
    public string Hash { get; set; } = "";
    public string Preview { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
}

// Lives in its own "mind" schema with its own migration history, next to Farm's tables in the same database.
public class MindDbContext(DbContextOptions<MindDbContext> options) : DbContext(options)
{
    public const string Schema = "mind";

    public DbSet<Memory> Memories => Set<Memory>();
    public DbSet<LinkedWorkspace> Workspaces => Set<LinkedWorkspace>();
    public DbSet<AccessKey> AccessKeys => Set<AccessKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Memory>(memory =>
        {
            memory.Property(m => m.Subject).HasMaxLength(200);
            memory.Property(m => m.Title).HasMaxLength(300);
            memory.Property(m => m.Workspace).HasMaxLength(200);
            memory.HasIndex(m => new { m.Subject, m.UpdatedAt });

            if (Database.IsNpgsql())
            {
                memory.HasGeneratedTsVectorColumn(m => m.SearchVector!, "english", m => new { m.Title, m.Content, m.Tags })
                    .HasIndex(m => m.SearchVector)
                    .HasMethod("GIN");
            }
            else
            {
                memory.Ignore(m => m.SearchVector);
            }
        });

        modelBuilder.Entity<AccessKey>(key =>
        {
            key.Property(k => k.Subject).HasMaxLength(200);
            key.Property(k => k.Name).HasMaxLength(100);
            key.Property(k => k.Hash).HasMaxLength(64);
            key.Property(k => k.Preview).HasMaxLength(16);
            key.HasIndex(k => k.Hash).IsUnique();
            key.HasIndex(k => k.Subject);
        });

        modelBuilder.Entity<LinkedWorkspace>(workspace =>
        {
            workspace.Property(w => w.Subject).HasMaxLength(200);
            workspace.Property(w => w.Name).HasMaxLength(200);
            workspace.HasIndex(w => new { w.Subject, w.Name }).IsUnique();
        });
    }
}
