using System;
using System.Collections.Generic;

namespace OpensourceLab.FileStorage.ServedServices
{
    /// <summary>
    /// Versioned file synchronization API contracts.
    /// All clients must use compatible versions of these contracts.
    /// See docs/SYNC_CONTRACTS.md for full specification.
    /// </summary>
    public static class FileSyncApi
    {
        /// <summary>API version following Semantic Versioning 2.0.0</summary>
        public const string ApiVersion = "1.0.0";

        /// <summary>Supported minimum client version (inclusive)</summary>
        public const string MinimumClientVersion = "1.0.0";

        /// <summary>Breaking changes between major versions</summary>
        public static class BreakingChanges
        {
            // Track major version breaking changes here for documentation
            // v1.x.x: Initial stable API
        }
    }

    public static class FileSyncEndpoints
    {
        public const string Handshake = "/api/sync/handshake";
        public const string PullChanges = "/api/sync/pull";
        public const string MetadataUpsert = "/api/sync/metadata-upsert";
        public const string UploadChunk = "/api/sync/upload-chunk";
        public const string UploadCommit = "/api/sync/upload-commit";
        public const string Tombstone = "/api/sync/tombstone";
    }

    public static class FileSyncHeaders
    {
        public const string AccessKey = FileStorageHeaders.AccessKey;
    }

    public class SyncHandshakeRequest
    {
        public string ClientId { get; set; } = string.Empty;
        public string ClientVersion { get; set; } = string.Empty;
        public string LocalFolderPath { get; set; } = string.Empty;
        public string KnownCursor { get; set; } = string.Empty;
    }

    public class SyncHandshakeResponse
    {
        public bool Succeeded { get; set; }
        public string Cursor { get; set; } = string.Empty;
        public string ServerId { get; set; } = string.Empty;
        public DateTime ServerUtc { get; set; }
        public List<string> AllowedPathWildcards { get; set; } = new();
        public string Error { get; set; } = string.Empty;
    }

    public class PullChangesRequest
    {
        public string Cursor { get; set; } = string.Empty;
        public int Limit { get; set; } = 200;
        public bool IncludeContent { get; set; } = true;
    }

    public class SyncFileChangeDto
    {
        public string Cursor { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string ContentHash { get; set; } = string.Empty;
        public long Size { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
        public bool IsDeleted { get; set; }
        public string ContentBase64 { get; set; } = string.Empty;
    }

    public class PullChangesResponse
    {
        public bool Succeeded { get; set; }
        public string Cursor { get; set; } = string.Empty;
        public List<SyncFileChangeDto> Changes { get; set; } = new();
        public string Error { get; set; } = string.Empty;
    }

    public class MetadataUpsertRequest
    {
        public string Path { get; set; } = string.Empty;
        public string ContentHash { get; set; } = string.Empty;
        public long Size { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }

    public class MetadataUpsertResponse
    {
        public bool Succeeded { get; set; }
        public bool Applied { get; set; }
        public string Cursor { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
    }

    public class UploadChunkRequest
    {
        public string UploadId { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public int ChunkIndex { get; set; }
        public string ContentBase64 { get; set; } = string.Empty;
        public string ContentHash { get; set; } = string.Empty;
        public long TotalSize { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }

    public class UploadChunkResponse
    {
        public bool Succeeded { get; set; }
        public bool ChunkAccepted { get; set; }
        public string UploadId { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
    }

    public class UploadCommitRequest
    {
        public string UploadId { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string ContentHash { get; set; } = string.Empty;
        public long Size { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
        public string ContentType { get; set; } = "application/octet-stream";
    }

    public class UploadCommitResponse
    {
        public bool Succeeded { get; set; }
        public bool Applied { get; set; }
        public string Cursor { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
    }

    public class TombstoneRequest
    {
        public string Path { get; set; } = string.Empty;
        public DateTime DeletedAtUtc { get; set; }
    }

    public class TombstoneResponse
    {
        public bool Succeeded { get; set; }
        public bool Applied { get; set; }
        public string Cursor { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
    }
}
