using System.Text.Json;

namespace Shed.Services
{
    public class FileSyncStateStore : ISyncStateStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        private static readonly TimeSpan StaleUploadThreshold = TimeSpan.FromHours(1);

        private readonly SemaphoreSlim _gate = new(1, 1);
        private readonly string _statePath;
        private readonly string _storageRoot;
        private SyncStateDocument? _state;

        public FileSyncStateStore(IConfiguration configuration)
        {
            _storageRoot = configuration["Storage:RootPath"] ?? @"/app/data/prodstorage";
            _statePath = Path.Combine(_storageRoot, ".sync", "sync-state.json");
        }

        public async Task<TResult> ReadAsync<TResult>(Func<SyncStateDocument, TResult> action, CancellationToken cancellationToken)
        {
            await _gate.WaitAsync(cancellationToken);
            try
            {
                var state = await GetOrLoadStateAsync(cancellationToken);
                return action(state);
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task<TResult> MutateAsync<TResult>(Func<SyncStateDocument, TResult> action, CancellationToken cancellationToken)
        {
            await _gate.WaitAsync(cancellationToken);
            try
            {
                var state = await GetOrLoadStateAsync(cancellationToken);
                EvictStaleUploads(state);
                var result = action(state);
                await PersistAsync(state, cancellationToken);
                return result;
            }
            finally
            {
                _gate.Release();
            }
        }

        private void EvictStaleUploads(SyncStateDocument state)
        {
            var cutoff = DateTime.UtcNow - StaleUploadThreshold;
            var stale = state.Uploads
                .Where(kv => kv.Value.UpdatedAtUtc < cutoff)
                .Select(kv => kv.Key)
                .ToList();

            foreach (var uploadId in stale)
            {
                state.Uploads.Remove(uploadId);
                var chunkFolder = Path.Combine(_storageRoot, ".sync", "chunks", uploadId);
                if (Directory.Exists(chunkFolder))
                {
                    try { Directory.Delete(chunkFolder, true); }
                    catch { /* best-effort: chunks may already be gone */ }
                }
            }
        }

        private async Task<SyncStateDocument> GetOrLoadStateAsync(CancellationToken cancellationToken)
        {
            if (_state is not null)
            {
                return _state;
            }

            var folder = Path.GetDirectoryName(_statePath)!;
            Directory.CreateDirectory(folder);

            if (!File.Exists(_statePath))
            {
                _state = new SyncStateDocument();
                await PersistAsync(_state, cancellationToken);
                return _state;
            }

            await using var stream = File.OpenRead(_statePath);
            _state = await JsonSerializer.DeserializeAsync<SyncStateDocument>(stream, JsonOptions, cancellationToken)
                ?? new SyncStateDocument();
            _state.Files ??= new Dictionary<string, SyncFileState>(StringComparer.OrdinalIgnoreCase);
            _state.Uploads ??= new Dictionary<string, UploadSessionState>(StringComparer.OrdinalIgnoreCase);
            _state.Changes ??= new List<SyncChangeState>();
            return _state;
        }

        private async Task PersistAsync(SyncStateDocument state, CancellationToken cancellationToken)
        {
            var tempPath = _statePath + ".tmp";
            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, state, JsonOptions, cancellationToken);
            }

            File.Move(tempPath, _statePath, true);
        }
    }
}


