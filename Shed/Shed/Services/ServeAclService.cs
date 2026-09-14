using Microsoft.EntityFrameworkCore;
using OpensourceLab.FileStorage.Domain;
using OpensourceLab.FileStorage.Meta;
using OpensourceLab.FileStorage.ServedServices;
using Shed.Data;

namespace Shed.Services
{
    public class ServeAclService : IServeAcl
    {
        private readonly VanillaDbContext _dbContext;

        public ServeAclService(VanillaDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IEnumerable<AccessKeyViewDto>> GetAccessKeysAsync(string userId, CancellationToken cancellationToken)
        {
            var normalizedUserId = NormalizeUser(userId);

            var keys = await _dbContext.AccessKeys
                .AsNoTracking()
                .Where(x => x.UserId == normalizedUserId)
                .OrderByDescending(x => x.UpdatedAt)
                .ToListAsync(cancellationToken);

            if (keys.Count == 0)
            {
                var seedKey = await CreateAccessKeyAsync(new CreateAccessKeyRequest(normalizedUserId, new[] { "/public/*", "/projects/*" }), cancellationToken);
                return new[] { seedKey };
            }

            return keys.Select(ToView);
        }

        public async Task<AccessKeyViewDto> CreateAccessKeyAsync(CreateAccessKeyRequest request, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var keyValue = GenerateAccessKey();
            var allowedPaths = NormalizePaths(request.AllowedPaths);
            var remark = GenerateAccessKeyRemark();

            var entity = new AccessKey(
                keyValue,
                NormalizeUser(request.UserId),
                string.Join(',', allowedPaths),
                true,
                true,
                remark,
                null,
                now,
                now);

            await _dbContext.AccessKeys.AddAsync(entity, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return ToView(entity);
        }

        public async Task UpdateAllowedPathsAsync(UpdateAllowedPathsRequest request, CancellationToken cancellationToken)
        {
            var key = await _dbContext.AccessKeys.FirstOrDefaultAsync(x => x.Key == request.AccessKey && x.UserId == NormalizeUser(request.UserId), cancellationToken);
            if (key is null)
            {
                return;
            }

            var updated = key with
            {
                FilePathWildCards = string.Join(',', NormalizePaths(request.AllowedPaths)),
                UpdatedAt = DateTime.UtcNow
            };

            _dbContext.AccessKeys.Update(updated);
            CopyCapabilityMetadata(key, updated);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task AddOrUpdateAccessKeyAsync(string accessKey, CancellationToken cancellationToken)
        {
            var existing = await _dbContext.AccessKeys.FirstOrDefaultAsync(x => x.Key == accessKey, cancellationToken);
            if (existing is null)
            {
                var now = DateTime.UtcNow;
                var entity = new AccessKey(accessKey, NormalizeUser(string.Empty), "/public/*", true, true, GenerateAccessKeyRemark(), null, now, now);
                await _dbContext.AccessKeys.AddAsync(entity, cancellationToken);
            }
            else
            {
                _dbContext.AccessKeys.Update(existing with { UpdatedAt = DateTime.UtcNow });
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task UpdateFileAclAsync(Guid fileItemId, CancellationToken cancellationToken)
        {
            var acl = await _dbContext.Acls.FirstOrDefaultAsync(x => x.FileItemId == fileItemId, cancellationToken);
            if (acl is null)
            {
                await _dbContext.Acls.AddAsync(new ACL(Guid.NewGuid(), fileItemId, new AclOwner(OwnerType.Public, string.Empty), AccessLevel.Read, DateTime.UtcNow, DateTime.UtcNow), cancellationToken);
            }
            else
            {
                _dbContext.Acls.Update(acl with { UpdatedAt = DateTime.UtcNow });
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task RevokeAccessKeyAsync(string accessKey, CancellationToken cancellationToken)
        {
            var existing = await _dbContext.AccessKeys.FirstOrDefaultAsync(x => x.Key == accessKey, cancellationToken);
            if (existing is null)
            {
                return;
            }

            _dbContext.AccessKeys.Remove(existing);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        private static AccessKeyViewDto ToView(AccessKey key)
        {
            var paths = NormalizePaths(key.FilePathWildCards.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            return new AccessKeyViewDto(key.Key, key.UserId, paths, key.CreatedAt, key.UpdatedAt);
        }

        private static string NormalizeUser(string userId)
        {
            return string.IsNullOrWhiteSpace(userId) ? "system" : userId.Trim();
        }

        private static IReadOnlyCollection<string> NormalizePaths(IEnumerable<string> paths)
        {
            var sourcePaths = paths?.Where(x => !string.IsNullOrWhiteSpace(x)).ToList() ?? new List<string>();
            if (sourcePaths.Count == 0)
            {
                sourcePaths.Add("/public/*");
            }

            return new PathWildcardScope(sourcePaths).Wildcards;
        }

        private static string GenerateAccessKey()
        {
            return $"ak_{Guid.NewGuid():N}";
        }

        private static string GenerateAccessKeyRemark()
        {
            return $"full-access-{Guid.NewGuid():N}";
        }

        private void CopyCapabilityMetadata(AccessKey source, AccessKey target)
        {
            var sourceEntry = _dbContext.Entry(source);
            var targetEntry = _dbContext.Entry(target);
            targetEntry.Property("CanRead").CurrentValue = sourceEntry.Property("CanRead").CurrentValue ?? true;
            targetEntry.Property("CanWrite").CurrentValue = sourceEntry.Property("CanWrite").CurrentValue ?? true;
            targetEntry.Property("Remark").CurrentValue = sourceEntry.Property("Remark").CurrentValue ?? GenerateAccessKeyRemark();
        }
    }
}


