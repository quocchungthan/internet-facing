using System.Text.Json;
using System.Text.Json.Serialization;

namespace AffineSync;

public interface ICheckpointStore
{
    Task<DateTimeOffset> ReadAsync(CancellationToken cancellationToken);
    Task WriteAsync(DateTimeOffset timestamp, CancellationToken cancellationToken);
    async Task<SyncCheckpoint> LoadAsync(CancellationToken cancellationToken) =>
        new() { CapturedAtUtc = await ReadAsync(cancellationToken) };
    Task SaveAsync(SyncCheckpoint checkpoint, CancellationToken cancellationToken) =>
        WriteAsync(checkpoint.CapturedAtUtc, cancellationToken);
}

public sealed record DocumentCheckpoint
{
    [JsonPropertyName("updatedAtUtc")] public required DateTimeOffset UpdatedAtUtc { get; init; }
    [JsonPropertyName("sha256")] public required string Sha256 { get; init; }
    [JsonPropertyName("contentHash")] public string? ContentHash { get; init; }
}

public sealed record SyncCheckpoint
{
    [JsonPropertyName("capturedAtUtc")] public required DateTimeOffset CapturedAtUtc { get; init; }
    [JsonPropertyName("documents")] public Dictionary<string, DocumentCheckpoint>? Documents { get; init; }
}

public sealed class CheckpointStore(string path) : ICheckpointStore
{
    public static readonly DateTimeOffset Baseline = new(1990, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private readonly string fullPath = Path.GetFullPath(path);

    public IDisposable AcquireLock()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        return new FileStream(fullPath + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }

    public async Task<DateTimeOffset> ReadAsync(CancellationToken cancellationToken) =>
        (await LoadAsync(cancellationToken)).CapturedAtUtc;

    public async Task<SyncCheckpoint> LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var checkpoint = await JsonSerializer.DeserializeAsync<SyncCheckpoint>(stream, cancellationToken: cancellationToken)
                ?? throw new InvalidDataException("Checkpoint is null.");
            if (checkpoint.CapturedAtUtc.Offset != TimeSpan.Zero || checkpoint.CapturedAtUtc < Baseline)
                throw new InvalidDataException("Checkpoint must contain a valid UTC timestamp.");
            if (checkpoint.Documents is not null)
                foreach (var (id, document) in checkpoint.Documents)
                    if (string.IsNullOrWhiteSpace(id) || document is null ||
                        document.UpdatedAtUtc.Offset != TimeSpan.Zero || document.UpdatedAtUtc < Baseline ||
                        document.Sha256 is null || document.Sha256.Length != 64 || !document.Sha256.All(Uri.IsHexDigit) ||
                        document.ContentHash is { Length: 0 or > 256 })
                        throw new InvalidDataException("Checkpoint contains an invalid document timestamp or hash.");
            return checkpoint;
        }
        catch (FileNotFoundException) { return new() { CapturedAtUtc = Baseline, Documents = [] }; }
        catch (DirectoryNotFoundException) { return new() { CapturedAtUtc = Baseline, Documents = [] }; }
    }

    public Task WriteAsync(DateTimeOffset timestamp, CancellationToken cancellationToken) =>
        SaveAsync(new() { CapturedAtUtc = timestamp }, cancellationToken);

    public async Task SaveAsync(SyncCheckpoint checkpoint, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var stagingPath = fullPath + "." + Guid.NewGuid().ToString("N") + ".pending";
        try
        {
            await using (var stream = new FileStream(stagingPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, checkpoint, cancellationToken: cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(stagingPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(stagingPath)) File.Delete(stagingPath);
        }
    }
}
