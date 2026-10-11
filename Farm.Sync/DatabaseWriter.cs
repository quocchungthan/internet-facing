using Farm.Data;
using Farm.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AffineSync;

public sealed class DatabaseWriter(Func<AuraFarming> createContext, Func<PortfolioDbContext>? createPortfolio = null)
    : IDatabaseWriter
{
    public async Task WriteAsync(IReadOnlyList<SourceDocument> documents, CancellationToken cancellationToken)
    {
        await WriteWebsiteAsync(documents, cancellationToken);
        if (!documents.Any(d => d.HasPortfolioRows)) return;
        if (createPortfolio is null)
            throw new InvalidOperationException("The snapshot contains portfolio rows but no portfolio context is configured.");
        // Separate transaction/history table; both writes are idempotent so a retry replays safely.
        await using var portfolio = createPortfolio();
        await PortfolioWriter.WriteAsync(portfolio, documents, cancellationToken);
    }

    private async Task WriteWebsiteAsync(IReadOnlyList<SourceDocument> documents, CancellationToken cancellationToken)
    {
        await using var database = createContext();
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (database.Database.IsNpgsql())
            await database.Database.ExecuteSqlRawAsync(
                """LOCK TABLE "WebsiteMetadata", "PlatformBookmarks" IN SHARE ROW EXCLUSIVE MODE""",
                cancellationToken);
        foreach (var document in documents)
        {
            foreach (var row in document.Metadata)
            {
                var existing = await database.WebsiteMetadata.FindAsync([row.Id], cancellationToken);
                if (existing is null)
                {
                    existing = new WebsiteMetadata { Id = row.Id, CreatedAtUtc = row.CreatedAtUtc.UtcDateTime };
                    database.WebsiteMetadata.Add(existing);
                }
                existing.DeveloperName = row.DeveloperName;
                existing.Title = row.Title;
                existing.Description = row.Description;
                existing.RepositoryUrl = row.RepositoryUrl;
                existing.SpecifiedIssueUrl = row.SpecifiedIssueUrl;
                existing.UpdatedAtUtc = row.UpdatedAtUtc.UtcDateTime;
            }
            foreach (var row in document.Bookmarks)
            {
                var existing = await database.PlatformBookmarks.FindAsync([row.Id], cancellationToken);
                if (existing is null)
                {
                    existing = new PlatformBookmark { Id = row.Id, CreatedAtUtc = row.CreatedAtUtc.UtcDateTime };
                    database.PlatformBookmarks.Add(existing);
                }
                existing.Platform = row.Platform;
                existing.Url = row.Url;
                existing.PlatformId = row.PlatformId;
                existing.Description = row.Description;
                existing.Color = row.Color;
                existing.DisplayOrder = row.DisplayOrder;
                existing.IsActive = row.IsActive;
            }
        }
        await database.SaveChangesAsync(cancellationToken);
        // Source-controlled IDs must not collide with later database-generated IDs.
        if (database.Database.IsNpgsql())
            await database.Database.ExecuteSqlRawAsync("""
                SELECT setval(pg_get_serial_sequence('"WebsiteMetadata"', 'Id'),
                    GREATEST((SELECT MAX("Id") FROM "WebsiteMetadata"),
                             (SELECT last_value FROM "WebsiteMetadata_Id_seq")), true);
                SELECT setval(pg_get_serial_sequence('"PlatformBookmarks"', 'Id'),
                    GREATEST((SELECT MAX("Id") FROM "PlatformBookmarks"),
                             (SELECT last_value FROM "PlatformBookmarks_Id_seq")), true);
                """, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
