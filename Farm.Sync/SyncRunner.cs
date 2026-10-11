using System.Security.Cryptography;
using System.Text.Json;

namespace AffineSync;

public interface IDatabaseWriter
{
    Task WriteAsync(IReadOnlyList<SourceDocument> documents, CancellationToken cancellationToken);
}

public sealed class SyncRunner(ISnapshotReader reader, ICheckpointStore state, IDatabaseWriter database)
{
    public Action<string, string> ReportSkipped { get; init; } = (id, reason) =>
        Console.Error.WriteLine($"{DateTimeOffset.UtcNow:O} Skipped document {id}: {reason}");

    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var checkpoint = await state.LoadAsync(cancellationToken);
        var snapshot = await reader.ReadAsync(cancellationToken);
        snapshot = snapshot.WithoutInvalidDocuments(ReportSkipped);
        var documents = checkpoint.Documents is null
            ? new Dictionary<string, DocumentCheckpoint>(StringComparer.Ordinal)
            : new Dictionary<string, DocumentCheckpoint>(checkpoint.Documents, StringComparer.Ordinal);
        var changed = new List<SourceDocument>();
        foreach (var document in snapshot.Documents)
        {
            var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(document)));
            if (documents.TryGetValue(document.Id, out var previous))
            {
                if (document.UpdatedAtUtc > previous.UpdatedAtUtc || hash != previous.Sha256 ||
                    document.ContentHash != previous.ContentHash)
                    changed.Add(document);
            }
            else if (document.UpdatedAtUtc > CheckpointStore.Baseline)
            {
                // Unknown documents replay once when migrating a legacy global checkpoint.
                changed.Add(document);
            }
            documents[document.Id] = new()
            {
                UpdatedAtUtc = document.UpdatedAtUtc, Sha256 = hash, ContentHash = document.ContentHash
            };
        }
        if (changed.Count == 0) return 0;

        await database.WriteAsync(changed, cancellationToken);
        // This watermark is informational; document-local timestamps and hashes gate writes.
        var nextCheckpoint = snapshot.Documents.Max(document => document.UpdatedAtUtc);
        if (nextCheckpoint > snapshot.CapturedAtUtc) nextCheckpoint = snapshot.CapturedAtUtc;
        if (nextCheckpoint < checkpoint.CapturedAtUtc) nextCheckpoint = checkpoint.CapturedAtUtc;
        await state.SaveAsync(new() { CapturedAtUtc = nextCheckpoint, Documents = documents }, cancellationToken);
        return changed.Count;
    }
}
