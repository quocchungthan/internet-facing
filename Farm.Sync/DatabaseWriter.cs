using Farm.Data;
using Microsoft.EntityFrameworkCore;

namespace AffineSync;

public sealed class DatabaseWriter(Func<AuraFarming> createContext, Func<PortfolioDbContext>? createPortfolio = null)
    : IDatabaseWriter
{
    public async Task WriteAsync(IReadOnlyList<SourceDocument> documents, CancellationToken cancellationToken)
    {
        if (documents.Any(d => d.Metadata.Count > 0))
        {
            await using var database = createContext();
            await WebsiteMetadataWriter.WriteAsync(database, documents, cancellationToken);
        }

        if (documents.Any(d => d.Bookmarks.Count > 0))
        {
            await using var database = createContext();
            await PlatformBookmarkWriter.WriteAsync(database, documents, cancellationToken);
        }

        if (documents.Any(d => d.Tools.Count > 0))
        {
            await using var database = createContext();
            await LocalhostToolWriter.WriteAsync(database, documents, cancellationToken);
        }

        if (documents.Any(d => d.HasPortfolioRows))
        {
            if (createPortfolio is null)
                throw new InvalidOperationException("The snapshot contains portfolio rows but no portfolio context is configured.");
            // Separate transaction/history table; writes are idempotent so a retry replays safely.
            await using var portfolio = createPortfolio();
            await PortfolioWriter.WriteAsync(portfolio, documents, cancellationToken);
        }
    }
}
