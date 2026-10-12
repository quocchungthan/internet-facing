using Microsoft.EntityFrameworkCore;

namespace Farm.Mind.Data;

public record McpUsageDay(DateOnly Date, long ReadBytes, long WriteBytes);

public class McpUsageStore(MindDbContext db)
{
    public async Task RecordAsync(string subject, long readBytes, long writeBytes, CancellationToken ct = default)
    {
        if (readBytes <= 0 && writeBytes <= 0) return;

        var day = DateOnly.FromDateTime(DateTime.UtcNow);
        if (db.Database.IsNpgsql())
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO mind."McpUsage" AS current_usage ("Subject", "Day", "ReadBytes", "WriteBytes")
                VALUES ({subject}, {day}, {readBytes}, {writeBytes})
                ON CONFLICT ("Subject", "Day") DO UPDATE SET
                    "ReadBytes" = current_usage."ReadBytes" + EXCLUDED."ReadBytes",
                    "WriteBytes" = current_usage."WriteBytes" + EXCLUDED."WriteBytes"
                """, ct);
            return;
        }

        var usage = await db.McpUsage.FindAsync([subject, day], ct);
        if (usage is null)
        {
            db.McpUsage.Add(new McpUsageDaily
            {
                Subject = subject,
                Day = day,
                ReadBytes = readBytes,
                WriteBytes = writeBytes
            });
        }
        else
        {
            usage.ReadBytes += readBytes;
            usage.WriteBytes += writeBytes;
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<McpUsageDay>> GetAsync(string subject, int days, CancellationToken ct = default)
    {
        var firstDay = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-(days - 1));
        return await db.McpUsage.AsNoTracking()
            .Where(u => u.Subject == subject && u.Day >= firstDay)
            .OrderBy(u => u.Day)
            .Select(u => new McpUsageDay(u.Day, u.ReadBytes, u.WriteBytes))
            .ToListAsync(ct);
    }
}
