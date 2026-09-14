using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OpensourceLab.FileStorage.ServedServices
{
    public interface IServeAcl
    {
        Task<IEnumerable<AccessKeyViewDto>> GetAccessKeysAsync(string userId, CancellationToken cancellationToken);
        Task<AccessKeyViewDto> CreateAccessKeyAsync(CreateAccessKeyRequest request, CancellationToken cancellationToken);
        Task UpdateAllowedPathsAsync(UpdateAllowedPathsRequest request, CancellationToken cancellationToken);
        Task AddOrUpdateAccessKeyAsync(string accessKey, CancellationToken cancellationToken);
        Task UpdateFileAclAsync(Guid fileItemId, CancellationToken cancellationToken);
        Task RevokeAccessKeyAsync(string accessKey, CancellationToken cancellationToken);
    }
}
