using OpensourceLab.FileStorage.Meta;
using System;
using System.Collections.Generic;
using System.Text;

namespace OpensourceLab.FileStorage.Domain
{
    public record ACL(
        Guid Id,
        Guid FileItemId,
        AclOwner Owner,
        AccessLevel Permissions,
        DateTime CreatedAt,
        DateTime UpdatedAt
    )
    {
        public ACL() : this(Guid.Empty, Guid.Empty, new AclOwner(), AccessLevel.None, DateTime.UtcNow, DateTime.UtcNow)
        {
        }
    }
}
