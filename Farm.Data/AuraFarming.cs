using Farm.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Farm.Data;

public class AuraFarming : DbContext
{
    public AuraFarming(DbContextOptions<AuraFarming> options) : base(options)
    {
    }

    public DbSet<WebsiteMetadata> WebsiteMetadata => Set<WebsiteMetadata>();
    public DbSet<PlatformBookmark> PlatformBookmarks => Set<PlatformBookmark>();
    public DbSet<LocalhostTool> LocalhostTools => Set<LocalhostTool>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<WebsiteMetadata>(entity =>
        {
            entity.ToTable("WebsiteMetadata");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.DeveloperName).HasMaxLength(128).IsRequired();
            entity.Property(e => e.Title).HasMaxLength(256).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(1024).IsRequired();
            entity.Property(e => e.RepositoryUrl).HasMaxLength(512).IsRequired();
            entity.Property(e => e.SpecifiedIssueUrl).HasMaxLength(512);

            entity.HasData(new WebsiteMetadata
            {
                Id = 1,
                DeveloperName = "quocchungthan",
                Title = "a Elder engineer learn to vibe code",
                Description = "if you are seeking for a software developer to talk to, it's me here",
                RepositoryUrl = "https://github.com/quocchungthan/internet-facing",
                CreatedAtUtc = new DateTime(2026, 10, 4, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAtUtc = new DateTime(2026, 10, 4, 0, 0, 0, DateTimeKind.Utc)
            });
        });

        modelBuilder.Entity<PlatformBookmark>(entity =>
        {
            entity.ToTable("PlatformBookmarks");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Platform).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Url).HasMaxLength(512).IsRequired();
            entity.Property(e => e.PlatformId).HasMaxLength(128).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(512).IsRequired();
            entity.Property(e => e.Color).HasMaxLength(32).IsRequired();
            entity.Property(e => e.DisplayOrder).HasDefaultValue(0);
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasData(
                new PlatformBookmark
                {
                    Id = 1,
                    Platform = "GitHub",
                    Url = "https://github.com/quocchungthan",
                    PlatformId = "quocchungthan",
                    Description = "GitHub repositories, PRs & code contributions",
                    Color = "#238636",
                    DisplayOrder = 1,
                    IsActive = true,
                    CreatedAtUtc = new DateTime(2026, 10, 4, 0, 0, 0, DateTimeKind.Utc)
                },
                new PlatformBookmark
                {
                    Id = 2,
                    Platform = "TikTok",
                    Url = "https://www.tiktok.com/@quocchungthan",
                    PlatformId = "quocchungthan",
                    Description = "Short-form tech demos and vibe coding feeds",
                    Color = "#ff0050",
                    DisplayOrder = 2,
                    IsActive = true,
                    CreatedAtUtc = new DateTime(2026, 10, 4, 0, 0, 0, DateTimeKind.Utc)
                },
                new PlatformBookmark
                {
                    Id = 3,
                    Platform = "Facebook",
                    Url = "https://www.facebook.com/quocchungthan",
                    PlatformId = "quocchungthan",
                    Description = "Community updates and social networking",
                    Color = "#1877f2",
                    DisplayOrder = 3,
                    IsActive = true,
                    CreatedAtUtc = new DateTime(2026, 10, 4, 0, 0, 0, DateTimeKind.Utc)
                },
                new PlatformBookmark
                {
                    Id = 4,
                    Platform = "Instagram",
                    Url = "https://www.instagram.com/quocchungthan",
                    PlatformId = "quocchungthan",
                    Description = "Dev stories, behind the scenes, and life feeds",
                    Color = "#e4405f",
                    DisplayOrder = 4,
                    IsActive = true,
                    CreatedAtUtc = new DateTime(2026, 10, 4, 0, 0, 0, DateTimeKind.Utc)
                },
                new PlatformBookmark
                {
                    Id = 5,
                    Platform = "Azure DevOps",
                    Url = "https://dev.azure.com/quocchungthan",
                    PlatformId = "quocchungthan",
                    Description = "Pipelines, work items, and backlog gigs",
                    Color = "#0078d4",
                    DisplayOrder = 5,
                    IsActive = true,
                    CreatedAtUtc = new DateTime(2026, 10, 4, 0, 0, 0, DateTimeKind.Utc)
                },
                new PlatformBookmark
                {
                    Id = 6,
                    Platform = "Gmail",
                    Url = "mailto:chung@eldervibe.dev",
                    PlatformId = "chung@eldervibe.dev",
                    Description = "Direct client inquiries & software gig proposals",
                    Color = "#ea4335",
                    DisplayOrder = 6,
                    IsActive = true,
                    CreatedAtUtc = new DateTime(2026, 10, 4, 0, 0, 0, DateTimeKind.Utc)
                }
            );
        });

        modelBuilder.Entity<LocalhostTool>(entity =>
        {
            entity.ToTable("LocalhostTools");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(128).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(1024).IsRequired();
            entity.Property(e => e.Category).HasMaxLength(64).IsRequired();
            entity.Property(e => e.DirectUrl).HasMaxLength(512).IsRequired();
            entity.Property(e => e.RepositoryUrl).HasMaxLength(512).IsRequired();
            entity.Property(e => e.IssueUrl).HasMaxLength(512);
            entity.Property(e => e.DisplayOrder).HasDefaultValue(0);
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasData(
                new LocalhostTool
                {
                    Id = 1,
                    Name = "Web & Repo Collector",
                    Description = "Locally collect web documentation and repo patterns for AI context retrieval",
                    Category = "SearchCollect",
                    DirectUrl = "http://localhost:5120",
                    RepositoryUrl = "https://github.com/quocchungthan/internet-facing",
                    IssueUrl = "https://github.com/quocchungthan/internet-facing/issues",
                    DisplayOrder = 1,
                    IsActive = true,
                    CreatedAtUtc = new DateTime(2026, 10, 13, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedAtUtc = new DateTime(2026, 10, 13, 0, 0, 0, DateTimeKind.Utc)
                },
                new LocalhostTool
                {
                    Id = 2,
                    Name = "Prompt & Markdown Transformer",
                    Description = "Transforms raw note AST and Yjs payloads into structured LLM prompts and markdown",
                    Category = "ConvertTransformCompose",
                    DirectUrl = "http://localhost:5121",
                    RepositoryUrl = "https://github.com/quocchungthan/internet-facing",
                    IssueUrl = "https://github.com/quocchungthan/internet-facing/issues",
                    DisplayOrder = 2,
                    IsActive = true,
                    CreatedAtUtc = new DateTime(2026, 10, 13, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedAtUtc = new DateTime(2026, 10, 13, 0, 0, 0, DateTimeKind.Utc)
                },
                new LocalhostTool
                {
                    Id = 3,
                    Name = "Realtime Stream Watcher",
                    Description = "Observes SSE streams, worker jobs and container health telemetry",
                    Category = "ObserveWatching",
                    DirectUrl = "http://localhost:5122",
                    RepositoryUrl = "https://github.com/quocchungthan/internet-facing",
                    IssueUrl = "https://github.com/quocchungthan/internet-facing/issues",
                    DisplayOrder = 3,
                    IsActive = true,
                    CreatedAtUtc = new DateTime(2026, 10, 13, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedAtUtc = new DateTime(2026, 10, 13, 0, 0, 0, DateTimeKind.Utc)
                },
                new LocalhostTool
                {
                    Id = 4,
                    Name = "Habit & Click Analyzer",
                    Description = "Calculates activity distributions and aggregates habit metrics across platforms",
                    Category = "CalculatingAnalysing",
                    DirectUrl = "http://localhost:5123",
                    RepositoryUrl = "https://github.com/quocchungthan/internet-facing",
                    IssueUrl = "https://github.com/quocchungthan/internet-facing/issues",
                    DisplayOrder = 4,
                    IsActive = true,
                    CreatedAtUtc = new DateTime(2026, 10, 13, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedAtUtc = new DateTime(2026, 10, 13, 0, 0, 0, DateTimeKind.Utc)
                }
            );
        });
    }
}
