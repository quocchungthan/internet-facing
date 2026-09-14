using System;

namespace OpensourceLab.FileStorage.Domain
{
    public record AccessKey (
        string Key,
        string UserId,
        string FilePathWildCards, // Separated by comma, e.g. /path/to/file1,/path/to/file2
        bool CanRead,
        bool CanWrite,
        string Remark,
        DateTime? ExpiresAt,
        DateTime CreatedAt,
        DateTime UpdatedAt
    );
}
