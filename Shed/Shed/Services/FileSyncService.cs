using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using OpensourceLab.FileStorage.Domain;
using OpensourceLab.FileStorage.ServedServices;
using Shed.Data;

namespace Shed.Services
{
    public interface IFileSyncService
    {
        Task<SyncHandshakeResponse> HandshakeAsync(SyncHandshakeRequest request, IReadOnlyCollection<string> allowedPathWildcards, CancellationToken cancellationToken);
        Task<PullChangesResponse> PullChangesAsync(PullChangesRequest request, string userRootPath, IReadOnlyCollection<string> allowedPathWildcards, CancellationToken cancellationToken);
        Task<MetadataUpsertResponse> MetadataUpsertAsync(MetadataUpsertRequest request, CancellationToken cancellationToken);
        Task<UploadChunkResponse> UploadChunkAsync(UploadChunkRequest request, string userRootPath, CancellationToken cancellationToken);
        Task<UploadCommitResponse> UploadCommitAsync(UploadCommitRequest request, string userRootPath, CancellationToken cancellationToken);
        Task<TombstoneResponse> TombstoneAsync(TombstoneRequest request, string userRootPath, CancellationToken cancellationToken);
    }

    public class FileSyncService : IFileSyncService
    {
        private readonly ISyncStateStore _stateStore;
        private readonly IConfiguration _configuration;
        private readonly VanillaDbContext _dbContext;

        public FileSyncService(ISyncStateStore stateStore, IConfiguration configuration, VanillaDbContext dbContext)
        {
            _stateStore = stateStore;
            _configuration = configuration;
            _dbContext = dbContext;
        }

        public Task<SyncHandshakeResponse> HandshakeAsync(SyncHandshakeRequest request, IReadOnlyCollection<string> allowedPathWildcards, CancellationToken cancellationToken)
        {
            return _stateStore.ReadAsync(state => new SyncHandshakeResponse
            {
                Succeeded = true,
                Cursor = ToCursor(state.LastSequence),
                ServerId = state.ServerId,
                ServerUtc = DateTime.UtcNow,
                AllowedPathWildcards = (allowedPathWildcards ?? Array.Empty<string>()).ToList()
            }, cancellationToken);
        }

        public Task<PullChangesResponse> PullChangesAsync(PullChangesRequest request, string userRootPath, IReadOnlyCollection<string> allowedPathWildcards, CancellationToken cancellationToken)
        {
            return _stateStore.ReadAsync(state =>
            {
                var cursor = ParseCursor(request.Cursor);
                var limit = request.Limit <= 0 ? 200 : Math.Min(request.Limit, 1000);
                var scope = new PathWildcardScope(allowedPathWildcards ?? Array.Empty<string>());
                var changes = state.Changes
                    .Where(x => x.Sequence > cursor)
                    .Where(x => scope.IsAllowed(x.Path))
                    .OrderBy(x => x.Sequence)
                    .Take(limit)
                    .ToList();

                var response = new PullChangesResponse
                {
                    Succeeded = true,
                    Cursor = changes.Count == 0 || changes.Count < limit
                        ? ToCursor(state.LastSequence)
                        : ToCursor(changes[^1].Sequence)
                };

                foreach (var change in changes)
                {
                    var dto = new SyncFileChangeDto
                    {
                        Cursor = ToCursor(change.Sequence),
                        Path = change.Path,
                        ContentHash = change.ContentHash,
                        Size = change.Size,
                        UpdatedAtUtc = change.UpdatedAtUtc,
                        IsDeleted = change.IsDeleted
                    };

                    if (request.IncludeContent && !change.IsDeleted)
                    {
                        var physicalPath = ResolvePhysicalPath(userRootPath, change.Path);
                        if (File.Exists(physicalPath))
                        {
                            dto.ContentBase64 = Convert.ToBase64String(File.ReadAllBytes(physicalPath));
                        }
                    }

                    response.Changes.Add(dto);
                }

                return response;
            }, cancellationToken);
        }

        public Task<MetadataUpsertResponse> MetadataUpsertAsync(MetadataUpsertRequest request, CancellationToken cancellationToken)
        {
            return _stateStore.MutateAsync(state =>
            {
                var normalizedPath = NormalizePath(request.Path);
                if (string.IsNullOrWhiteSpace(normalizedPath))
                {
                    return new MetadataUpsertResponse
                    {
                        Succeeded = false,
                        Error = "Path is required.",
                        Cursor = ToCursor(state.LastSequence)
                    };
                }

                if (state.Files.TryGetValue(normalizedPath, out var existing) &&
                    !existing.IsDeleted &&
                    string.Equals(existing.ContentHash, request.ContentHash, StringComparison.OrdinalIgnoreCase) &&
                    existing.Size == request.Size)
                {
                    return new MetadataUpsertResponse
                    {
                        Succeeded = true,
                        Applied = false,
                        Cursor = ToCursor(state.LastSequence)
                    };
                }

                return new MetadataUpsertResponse
                {
                    Succeeded = true,
                    Applied = true,
                    Cursor = ToCursor(state.LastSequence)
                };
            }, cancellationToken);
        }

        public Task<UploadChunkResponse> UploadChunkAsync(UploadChunkRequest request, string userRootPath, CancellationToken cancellationToken)
        {
            return _stateStore.MutateAsync(state =>
            {
                if (request.ChunkIndex < 0)
                {
                    return new UploadChunkResponse { Succeeded = false, Error = "Chunk index must be >= 0.", UploadId = request.UploadId };
                }

                var uploadId = string.IsNullOrWhiteSpace(request.UploadId)
                    ? Guid.NewGuid().ToString("N")
                    : request.UploadId.Trim();

                if (!state.Uploads.TryGetValue(uploadId, out var session))
                {
                    session = new UploadSessionState
                    {
                        UploadId = uploadId,
                        Path = NormalizePath(request.Path),
                        ContentHash = request.ContentHash,
                        TotalSize = request.TotalSize,
                        UpdatedAtUtc = request.UpdatedAtUtc == default ? DateTime.UtcNow : request.UpdatedAtUtc,
                        UserRootPath = userRootPath
                    };
                    state.Uploads[uploadId] = session;
                }
                else if (!string.Equals(session.Path, NormalizePath(request.Path), StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(session.ContentHash, request.ContentHash, StringComparison.OrdinalIgnoreCase)
                    || session.TotalSize != request.TotalSize
                    || !string.Equals(session.UserRootPath, userRootPath, StringComparison.OrdinalIgnoreCase))
                {
                    return new UploadChunkResponse
                    {
                        Succeeded = false,
                        Error = "Upload session mismatch.",
                        UploadId = uploadId
                    };
                }

                var chunkPath = GetChunkPath(uploadId, request.ChunkIndex);
                Directory.CreateDirectory(Path.GetDirectoryName(chunkPath)!);
                if (!File.Exists(chunkPath))
                {
                    var bytes = Convert.FromBase64String(request.ContentBase64);
                    File.WriteAllBytes(chunkPath, bytes);
                }

                if (!session.ChunkIndexes.Contains(request.ChunkIndex))
                {
                    session.ChunkIndexes.Add(request.ChunkIndex);
                    session.ChunkIndexes = session.ChunkIndexes.Distinct().OrderBy(x => x).ToList();
                }

                session.UpdatedAtUtc = request.UpdatedAtUtc == default ? DateTime.UtcNow : request.UpdatedAtUtc;

                return new UploadChunkResponse
                {
                    Succeeded = true,
                    ChunkAccepted = true,
                    UploadId = uploadId
                };
            }, cancellationToken);
        }

        public async Task<UploadCommitResponse> UploadCommitAsync(UploadCommitRequest request, string userRootPath, CancellationToken cancellationToken)
        {
            var commitResult = await _stateStore.MutateAsync(state =>
            {
                if (!state.Uploads.TryGetValue(request.UploadId, out var session))
                {
                    return new UploadCommitResponse { Succeeded = false, Error = "Upload session not found.", Cursor = ToCursor(state.LastSequence) };
                }

                var normalizedPath = NormalizePath(request.Path);
                if (!string.Equals(session.Path, normalizedPath, StringComparison.OrdinalIgnoreCase))
                {
                    return new UploadCommitResponse { Succeeded = false, Error = "Upload path mismatch.", Cursor = ToCursor(state.LastSequence) };
                }

                if (session.ChunkIndexes.Count == 0)
                {
                    return new UploadCommitResponse { Succeeded = false, Error = "No chunks uploaded.", Cursor = ToCursor(state.LastSequence) };
                }

                var finalPath = ResolvePhysicalPath(userRootPath, normalizedPath);
                Directory.CreateDirectory(Path.GetDirectoryName(finalPath)!);
                using (var output = File.Create(finalPath))
                {
                    foreach (var index in session.ChunkIndexes.OrderBy(x => x))
                    {
                        var chunkPath = GetChunkPath(session.UploadId, index);
                        if (!File.Exists(chunkPath))
                        {
                            return new UploadCommitResponse { Succeeded = false, Error = "Missing chunk file.", Cursor = ToCursor(state.LastSequence) };
                        }

                        using var input = File.OpenRead(chunkPath);
                        input.CopyTo(output);
                    }
                }

                var hash = ComputeFileHash(finalPath);
                if (!string.IsNullOrWhiteSpace(request.ContentHash) && !string.Equals(hash, request.ContentHash, StringComparison.OrdinalIgnoreCase))
                {
                    return new UploadCommitResponse { Succeeded = false, Error = "Content hash mismatch.", Cursor = ToCursor(state.LastSequence) };
                }

                var fileInfo = new FileInfo(finalPath);
                var updatedAt = request.UpdatedAtUtc == default ? DateTime.UtcNow : request.UpdatedAtUtc;
                state.Files[normalizedPath] = new SyncFileState
                {
                    Path = normalizedPath,
                    ContentHash = hash,
                    Size = fileInfo.Length,
                    UpdatedAtUtc = updatedAt,
                    IsDeleted = false
                };

                var sequence = NextSequence(state);
                state.Changes.Add(new SyncChangeState
                {
                    Sequence = sequence,
                    Path = normalizedPath,
                    ContentHash = hash,
                    Size = fileInfo.Length,
                    UpdatedAtUtc = updatedAt,
                    IsDeleted = false
                });

                CleanupUploadChunks(session.UploadId);
                state.Uploads.Remove(session.UploadId);

                return new UploadCommitResponse
                {
                    Succeeded = true,
                    Applied = true,
                    Cursor = ToCursor(sequence)
                };
            }, cancellationToken);

            if (!commitResult.Succeeded)
            {
                return commitResult;
            }

            var normalizedPathForDb = NormalizePath(request.Path);
            var physicalPathForDb = ResolvePhysicalPath(userRootPath, normalizedPathForDb);
            var fileInfoForDb = new FileInfo(physicalPathForDb);
            var now = DateTime.UtcNow;

            var existing = await _dbContext.FileItems.FirstOrDefaultAsync(x => x.Path == normalizedPathForDb, cancellationToken);
            if (existing is null)
            {
                await _dbContext.FileItems.AddAsync(new FileItem(
                    Guid.NewGuid(),
                    normalizedPathForDb,
                    physicalPathForDb,
                    string.IsNullOrWhiteSpace(request.ContentType) ? "application/octet-stream" : request.ContentType,
                    fileInfoForDb.Length,
                    now,
                    now), cancellationToken);
            }
            else
            {
                _dbContext.FileItems.Update(existing with
                {
                    ActualPath = physicalPathForDb,
                    ContentType = string.IsNullOrWhiteSpace(request.ContentType) ? existing.ContentType : request.ContentType,
                    Size = fileInfoForDb.Length,
                    UpdatedAt = now
                });
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            return commitResult;
        }

        public async Task<TombstoneResponse> TombstoneAsync(TombstoneRequest request, string userRootPath, CancellationToken cancellationToken)
        {
            var response = await _stateStore.MutateAsync(state =>
            {
                var normalizedPath = NormalizePath(request.Path);
                if (string.IsNullOrWhiteSpace(normalizedPath))
                {
                    return new TombstoneResponse { Succeeded = false, Error = "Path is required.", Cursor = ToCursor(state.LastSequence) };
                }

                if (state.Files.TryGetValue(normalizedPath, out var existing) && existing.IsDeleted)
                {
                    return new TombstoneResponse { Succeeded = true, Applied = false, Cursor = ToCursor(state.LastSequence) };
                }

                var deletedAt = request.DeletedAtUtc == default ? DateTime.UtcNow : request.DeletedAtUtc;
                state.Files[normalizedPath] = new SyncFileState
                {
                    Path = normalizedPath,
                    ContentHash = string.Empty,
                    Size = 0,
                    UpdatedAtUtc = deletedAt,
                    IsDeleted = true
                };

                var sequence = NextSequence(state);
                state.Changes.Add(new SyncChangeState
                {
                    Sequence = sequence,
                    Path = normalizedPath,
                    ContentHash = string.Empty,
                    Size = 0,
                    UpdatedAtUtc = deletedAt,
                    IsDeleted = true
                });

                return new TombstoneResponse
                {
                    Succeeded = true,
                    Applied = true,
                    Cursor = ToCursor(sequence)
                };
            }, cancellationToken);

            if (!response.Succeeded)
            {
                return response;
            }

            var normalizedPathForFs = NormalizePath(request.Path);
            var physicalPath = ResolvePhysicalPath(userRootPath, normalizedPathForFs);
            if (File.Exists(physicalPath))
            {
                File.Delete(physicalPath);
            }

            var existing = await _dbContext.FileItems.FirstOrDefaultAsync(x => x.Path == normalizedPathForFs, cancellationToken);
            if (existing is not null)
            {
                _dbContext.FileItems.Remove(existing);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            return response;
        }

        private static long NextSequence(SyncStateDocument state)
        {
            state.LastSequence++;
            return state.LastSequence;
        }

        private static string ToCursor(long sequence)
        {
            return $"seq-{sequence:D20}";
        }

        private static long ParseCursor(string cursor)
        {
            if (string.IsNullOrWhiteSpace(cursor))
            {
                return 0;
            }

            var value = cursor.Trim();
            if (value.StartsWith("seq-", StringComparison.OrdinalIgnoreCase))
            {
                value = value[4..];
            }

            return long.TryParse(value, out var parsed) ? parsed : 0;
        }

        private static string NormalizePath(string? rawPath)
        {
            if (string.IsNullOrWhiteSpace(rawPath))
            {
                return string.Empty;
            }

            var value = rawPath.Replace('\\', '/').Trim();
            if (!value.StartsWith('/'))
            {
                value = "/" + value;
            }

            while (value.Contains("//", StringComparison.Ordinal))
            {
                value = value.Replace("//", "/", StringComparison.Ordinal);
            }

            return value;
        }

        private static string ResolvePhysicalPath(string userRootPath, string virtualPath)
        {
            var relative = virtualPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var combined = Path.GetFullPath(Path.Combine(userRootPath, relative));
            var root = Path.GetFullPath(userRootPath);
            if (!combined.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Path traversal is not allowed.");
            }

            return combined;
        }

        private static string ComputeFileHash(string path)
        {
            using var stream = File.OpenRead(path);
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(stream);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private string GetChunkPath(string uploadId, int index)
        {
            var storageRoot = _configuration["Storage:RootPath"] ?? @"/app/data/prodstorage";
            return Path.Combine(storageRoot, ".sync", "chunks", uploadId, $"{index:D8}.chunk");
        }

        private void CleanupUploadChunks(string uploadId)
        {
            var storageRoot = _configuration["Storage:RootPath"] ?? @"/app/data/prodstorage";
            var folder = Path.Combine(storageRoot, ".sync", "chunks", uploadId);
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
        }
    }
}


