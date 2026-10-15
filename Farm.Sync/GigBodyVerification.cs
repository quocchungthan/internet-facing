using Farm.Data;
using Microsoft.EntityFrameworkCore;

namespace AffineSync;

public static class GigBodyVerification
{
    public static async Task VerifyAsync(PortfolioDbContext database, Snapshot snapshot, TextWriter output,
        CancellationToken cancellationToken)
    {
        snapshot.Validate();
        var expected = snapshot.Documents.SelectMany(document => document.Gigs).ToList();
        if (expected.Count == 0)
        {
            await output.WriteLineAsync("INFO  No Gig bodies were exported for verification.");
            return;
        }
        if (expected.Any(row => row.BodyMarkdown is null))
            throw new InvalidDataException("A source Gig has no BodyMarkdown field; rebuild the reader.");

        var ids = expected.Select(row => row.SourceDocId).ToArray();
        var actual = await database.GigProblems.AsNoTracking()
            .Where(gig => gig.SourceDocId != null && ids.Contains(gig.SourceDocId))
            .ToDictionaryAsync(gig => gig.SourceDocId!, gig => gig.BodyMarkdown, cancellationToken);
        var mismatches = expected.Count(row =>
            !actual.TryGetValue(row.SourceDocId, out var body) ||
            !string.Equals(body, row.BodyMarkdown, StringComparison.Ordinal));
        if (mismatches > 0)
            throw new InvalidDataException($"{mismatches} of {expected.Count} stored Gig Markdown bodies differ from the source; wait for a sync pass and retry.");

        await output.WriteLineAsync($"PASS  All {expected.Count} stored Gig BodyMarkdown values exactly match the AFFiNE export ({expected.Sum(row => (long)row.BodyMarkdown!.Length)} characters).");
    }
}
