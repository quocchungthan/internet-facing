using Farm.Data;
using Farm.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AffineSync;

public static class PlatformBookmarkWriter
{
    public static async Task WriteAsync(AuraFarming database, IReadOnlyList<SourceDocument> documents,
        CancellationToken cancellationToken)
    {
        var rows = documents.SelectMany(d => d.Bookmarks).ToList();
        if (rows.Count == 0) return;

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (database.Database.IsNpgsql())
            await database.Database.ExecuteSqlRawAsync(
                """LOCK TABLE "PlatformBookmarks" IN SHARE ROW EXCLUSIVE MODE""",
                cancellationToken);

        foreach (var row in rows)
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

        await database.SaveChangesAsync(cancellationToken);

        if (database.Database.IsNpgsql())
            await database.Database.ExecuteSqlRawAsync("""
                SELECT setval(pg_get_serial_sequence('"PlatformBookmarks"', 'Id'),
                    GREATEST((SELECT MAX("Id") FROM "PlatformBookmarks"),
                             (SELECT last_value FROM "PlatformBookmarks_Id_seq")), true);
                """, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }
}
