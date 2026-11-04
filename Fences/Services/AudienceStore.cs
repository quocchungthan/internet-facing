using Fences.Data;
using Microsoft.EntityFrameworkCore;

namespace Fences.Services;

public sealed class AudienceStore(AudienceDbContext db)
{
    public async Task RecordAsync(string clientId, string subject, string username, CancellationToken ct)
    {
        var day = DateTime.UtcNow.ToString("yyyy-MM-dd");
        // One observation per user/application/day; retries and repeated authorization do not inflate users.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Visits" ("ClientId", "Subject", "Username", "Day")
            VALUES ({clientId}, {subject}, {username}, {day})
            ON CONFLICT ("ClientId", "Subject", "Day") DO UPDATE SET "Username" = excluded."Username"
            """, ct);
    }

    public Task<object> ReadAsync(string clientId, int days, CancellationToken ct) =>
        ReadAsync(clientId, days, allClients: false, ct);

    public Task<object> ReadAllAsync(int days, CancellationToken ct) =>
        ReadAsync(clientId: null, days, allClients: true, ct);

    private async Task<object> ReadAsync(string? clientId, int days, bool allClients, CancellationToken ct)
    {
        var through = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var from = DateTime.UtcNow.Date.AddDays(1 - days).ToString("yyyy-MM-dd");
        var query = db.Visits.AsNoTracking();
        if (!allClients)
            query = query.Where(visit => visit.ClientId == clientId);
        var visits = await query.ToListAsync(ct);
        var users = visits.GroupBy(visit => visit.Subject).Select(group =>
        {
            var ordered = group.OrderBy(visit => visit.Day, StringComparer.Ordinal)
                .ThenBy(visit => visit.ClientId, StringComparer.Ordinal).ToArray();
            return new
            {
                subject = group.Key,
                username = ordered[^1].Username,
                firstSeen = ordered[0].Day,
                lastSeen = ordered[^1].Day,
                observedDays = ordered.Select(visit => visit.Day).Distinct(StringComparer.Ordinal).Count(),
                clients = ordered.Select(visit => visit.ClientId).Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                seenInPeriod = ordered.Any(visit => string.CompareOrdinal(visit.Day, from) >= 0 && string.CompareOrdinal(visit.Day, through) <= 0)
            };
        }).OrderBy(user => user.username, StringComparer.Ordinal).ToArray();
        return new
        {
            source = "fences",
            clientId = allClients ? "all" : clientId,
            from,
            through,
            totalUsers = users.Length,
            newUsers = users.Count(user => string.CompareOrdinal(user.firstSeen, from) >= 0),
            returningUsers = users.Count(user => string.CompareOrdinal(user.firstSeen, from) < 0 && user.seenInPeriod),
            growth = users.Where(user => string.CompareOrdinal(user.firstSeen, from) >= 0)
                .GroupBy(user => user.firstSeen).OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => new { date = group.Key, users = group.Count() }),
            applications = visits.GroupBy(visit => visit.ClientId)
                .Select(group => new { clientId = group.Key, totalUsers = group.Select(visit => visit.Subject).Distinct().Count() })
                .OrderBy(application => application.clientId, StringComparer.Ordinal),
            users,
            coverage = allClients
                ? "Unique users observed across all recorded app authorizations since tracking was enabled. First seen is not original registration; observed days are not login counts or online presence."
                : "Observed app authorizations since tracking was enabled. First seen is not original registration; observed days are not login counts or online presence."
        };
    }
}
