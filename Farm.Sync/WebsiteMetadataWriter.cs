using Farm.Data;
using Farm.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AffineSync;

public static class WebsiteMetadataWriter
{
    public static async Task WriteAsync(AuraFarming database, IReadOnlyList<SourceDocument> documents,
        CancellationToken cancellationToken)
    {
        var rows = documents.SelectMany(d => d.Metadata).ToList();
        if (rows.Count == 0) return;

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (database.Database.IsNpgsql())
            await database.Database.ExecuteSqlRawAsync(
                """LOCK TABLE "WebsiteMetadata" IN SHARE ROW EXCLUSIVE MODE""",
                cancellationToken);

        foreach (var row in rows)
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

        await database.SaveChangesAsync(cancellationToken);

        if (database.Database.IsNpgsql())
            await database.Database.ExecuteSqlRawAsync("""
                SELECT setval(pg_get_serial_sequence('"WebsiteMetadata"', 'Id'),
                    GREATEST((SELECT MAX("Id") FROM "WebsiteMetadata"),
                             (SELECT last_value FROM "WebsiteMetadata_Id_seq")), true);
                """, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }
}
