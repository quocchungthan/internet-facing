using System;
using System.Collections.Generic;

namespace Shed.Services
{
    public class SyncStateDocument
    {
        public string ServerId { get; set; } = Guid.NewGuid().ToString("N");
        public long LastSequence { get; set; }
        public Dictionary<string, SyncFileState> Files { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public List<SyncChangeState> Changes { get; set; } = new();
        public Dictionary<string, UploadSessionState> Uploads { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public class SyncFileState
    {
        public string Path { get; set; } = string.Empty;
        public string ContentHash { get; set; } = string.Empty;
        public long Size { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
        public bool IsDeleted { get; set; }
    }

    public class SyncChangeState
    {
        public long Sequence { get; set; }
        public string Path { get; set; } = string.Empty;
        public string ContentHash { get; set; } = string.Empty;
        public long Size { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
        public bool IsDeleted { get; set; }
    }

    public class UploadSessionState
    {
        public string UploadId { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string ContentHash { get; set; } = string.Empty;
        public long TotalSize { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
        public string UserRootPath { get; set; } = string.Empty;
        public List<int> ChunkIndexes { get; set; } = new();
    }
}


