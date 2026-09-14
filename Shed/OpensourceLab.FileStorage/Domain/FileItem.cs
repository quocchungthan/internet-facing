using System;

namespace OpensourceLab.FileStorage.Domain
{
    public record FileItem(
        Guid Id,
        string Path,
        string ActualPath,
        string ContentType,
        long Size,
        DateTime CreatedAt,
        DateTime UpdatedAt
    );
}
