using Farm.Data;
using Farm.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AffineSync;

public static class LocalhostToolWriter
{
    public static async Task WriteAsync(AuraFarming database, IReadOnlyList<SourceDocument> documents,
        CancellationToken cancellationToken)
    {
        var rows = documents.SelectMany(d => d.Tools).ToList();
        if (rows.Count == 0) return;

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (database.Database.IsNpgsql())
            await database.Database.ExecuteSqlRawAsync(
                """LOCK TABLE "LocalhostTools" IN SHARE ROW EXCLUSIVE MODE""",
                cancellationToken);

        foreach (var row in rows)
        {
            var existing = await database.LocalhostTools.FindAsync([row.Id], cancellationToken);
            if (existing is null)
            {
                existing = new LocalhostTool { Id = row.Id, CreatedAtUtc = row.CreatedAtUtc.UtcDateTime };
                database.LocalhostTools.Add(existing);
            }
            existing.Name = row.Name;
            existing.Description = row.Description;
            existing.Category = row.Category;
            existing.DirectUrl = row.DirectUrl;
            existing.RepositoryUrl = row.RepositoryUrl ?? string.Empty;
            existing.IssueUrl = row.IssueUrl;
            existing.DisplayOrder = row.DisplayOrder;
            existing.IsActive = row.IsActive;
            existing.UpdatedAtUtc = DateTime.UtcNow;
        }

        await database.SaveChangesAsync(cancellationToken);

        if (database.Database.IsNpgsql())
            await database.Database.ExecuteSqlRawAsync("""
                SELECT setval(pg_get_serial_sequence('"LocalhostTools"', 'Id'),
                    GREATEST((SELECT MAX("Id") FROM "LocalhostTools"),
                             (SELECT last_value FROM "LocalhostTools_Id_seq")), true);
                """, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }
}
