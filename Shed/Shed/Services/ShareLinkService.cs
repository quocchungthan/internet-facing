using Microsoft.EntityFrameworkCore;
using OpensourceLab.FileStorage.ServedServices;
using Shed.Data;
using System.Security.Cryptography;
using System.Text;

namespace Shed.Services
{
    public class ShareLinkService
    {
        private readonly IServeAcl _serveAcl;
        private readonly VanillaDbContext _dbContext;
        private readonly ILogger<ShareLinkService> _logger;

        public ShareLinkService(
            IServeAcl serveAcl,
            VanillaDbContext dbContext,
            ILogger<ShareLinkService> logger)
        {
            _serveAcl = serveAcl;
            _dbContext = dbContext;
            _logger = logger;
        }

        /// <summary>
        /// Create a share link (readonly AccessKey) for a specific path with expiration.
        /// </summary>
        public async Task<ShareLinkDto> CreateShareLinkAsync(
            string userId,
            string targetPath,
            int expirationMinutes = 1440,  // 24 hours default
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(targetPath))
                throw new ArgumentException("Target path required", nameof(targetPath));

            if (expirationMinutes <= 0)
                throw new ArgumentException("Expiration must be positive", nameof(expirationMinutes));

            // Create a readonly access key for the specific path
            var createRequest = new CreateAccessKeyRequest(userId, new[] { targetPath });
            var created = await _serveAcl.CreateAccessKeyAsync(createRequest, cancellationToken);

            var expiresAt = DateTime.UtcNow.AddMinutes(expirationMinutes);

            // Update with readonly and expiration settings
            await UpdateShareLinkAsync(
                created.Key,
                userId,
                canRead: true,
                canWrite: false,  // Readonly
                remark: $"share-link-{Path.GetFileName(targetPath)}",
                expiresAt: expiresAt,
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Created share link for path {Path} expiring at {ExpiresAt}",
                targetPath,
                expiresAt);

            // Piggyback: sweep other expired share links for this user on each new creation.
            await CleanupExpiredLinksAsync(userId, cancellationToken);

            return new ShareLinkDto
            {
                AccessKey = created.Key,
                TargetPath = targetPath,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = expiresAt,
                IsExpired = false
            };
        }

        /// <summary>
        /// Update share link properties (expiration, remark).
        /// </summary>
        private async Task UpdateShareLinkAsync(
            string accessKey,
            string userId,
            bool canRead,
            bool canWrite,
            string remark,
            DateTime? expiresAt,
            CancellationToken cancellationToken = default)
        {
            var entity = await _dbContext.AccessKeys.FirstOrDefaultAsync(
                x => x.Key == accessKey && x.UserId == userId,
                cancellationToken);

            if (entity == null)
                throw new InvalidOperationException("Access key not found");

            _dbContext.Entry(entity).State = EntityState.Detached;

            var updated = entity with { UpdatedAt = DateTime.UtcNow };
            var entry = _dbContext.Attach(updated);

            entry.Property("CanRead").CurrentValue = canRead;
            entry.Property("CanWrite").CurrentValue = canWrite;
            entry.Property("Remark").CurrentValue = remark;
            entry.Property("ExpiresAt").CurrentValue = expiresAt;

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// Get all share links for a user.
        /// </summary>
        public async Task<IEnumerable<ShareLinkDto>> GetShareLinksAsync(
            string userId,
            CancellationToken cancellationToken = default)
        {
            var links = await _dbContext.AccessKeys
                .AsNoTracking()
                .Where(x => x.UserId == userId
                    && EF.Property<string>(x, "Remark").StartsWith("share-link-"))
                .Select(x => new ShareLinkDto
                {
                    AccessKey = x.Key,
                    TargetPath = x.FilePathWildCards,
                    CreatedAt = x.CreatedAt,
                    ExpiresAt = (DateTime?)EF.Property<DateTime?>(x, "ExpiresAt"),
                    IsExpired = (DateTime?)EF.Property<DateTime?>(x, "ExpiresAt") < DateTime.UtcNow
                })
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync(cancellationToken);

            return links;
        }

        /// <summary>
        /// Get share links for a specific path.
        /// </summary>
        public async Task<IEnumerable<ShareLinkDto>> GetShareLinksForPathAsync(
            string userId,
            string targetPath,
            CancellationToken cancellationToken = default)
        {
            var links = await _dbContext.AccessKeys
                .AsNoTracking()
                .Where(x => x.UserId == userId
                    && x.FilePathWildCards == targetPath
                    && EF.Property<string>(x, "Remark").StartsWith("share-link-"))
                .Select(x => new ShareLinkDto
                {
                    AccessKey = x.Key,
                    TargetPath = x.FilePathWildCards,
                    CreatedAt = x.CreatedAt,
                    ExpiresAt = (DateTime?)EF.Property<DateTime?>(x, "ExpiresAt"),
                    IsExpired = (DateTime?)EF.Property<DateTime?>(x, "ExpiresAt") < DateTime.UtcNow
                })
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync(cancellationToken);

            return links;
        }

        /// <summary>
        /// Validate and get details of a share link.
        /// Automatically deletes the key if it is expired (trigger-based cleanup).
        /// </summary>
        public async Task<ShareLinkValidation> ValidateShareLinkAsync(
    string accessKey,
    CancellationToken cancellationToken = default)
{
    var key = await _dbContext.AccessKeys
        .FirstOrDefaultAsync(x => x.Key == accessKey, cancellationToken);

    if (key == null)
    {
        return new ShareLinkValidation
        {
            IsValid = false,
            Reason = "Invalid access key"
        };
    }

    // Access normal properties directly
    var remark = key.Remark;
    if (string.IsNullOrEmpty(remark) || !remark.StartsWith("share-link-"))
    {
        return new ShareLinkValidation
        {
            IsValid = false,
            Reason = "Not a share link"
        };
    }

    var expiresAt = key.ExpiresAt;
    if (expiresAt.HasValue && expiresAt < DateTime.UtcNow)
    {
        _dbContext.AccessKeys.Remove(key);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Deleted expired share link {AccessKey} (expired {ExpiresAt})",
            accessKey, expiresAt);

        return new ShareLinkValidation
        {
            IsValid = false,
            Reason = "Share link expired",
            ExpiredAt = expiresAt
        };
    }

    if (!key.CanRead)
    {
        return new ShareLinkValidation
        {
            IsValid = false,
            Reason = "Access key is read-disabled"
        };
    }

    return new ShareLinkValidation
    {
        IsValid = true,
        AccessKey = key.Key,
        TargetPath = key.FilePathWildCards,
        CreatedAt = key.CreatedAt,
        ExpiresAt = expiresAt
    };
}

        /// <summary>
        /// Revoke a share link.
        /// </summary>
        public async Task RevokeShareLinkAsync(
            string accessKey,
            string userId,
            CancellationToken cancellationToken = default)
        {
            var key = await _dbContext.AccessKeys
                .FirstOrDefaultAsync(x => x.Key == accessKey && x.UserId == userId, cancellationToken);

            if (key == null)
                throw new InvalidOperationException("Share link not found");

            _dbContext.AccessKeys.Remove(key);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Revoked share link {AccessKey}", accessKey);
        }

        /// <summary>
        /// Clean up expired share links for a user.
        /// </summary>
        public async Task CleanupExpiredLinksAsync(
            string userId,
            CancellationToken cancellationToken = default)
        {
            var expired = await _dbContext.AccessKeys
                .Where(x => x.UserId == userId
                    && EF.Property<string>(x, "Remark").StartsWith("share-link-")
                    && (DateTime?)EF.Property<DateTime?>(x, "ExpiresAt") < DateTime.UtcNow)
                .ToListAsync(cancellationToken);

            if (expired.Any())
            {
                _dbContext.AccessKeys.RemoveRange(expired);
                await _dbContext.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Cleaned up {Count} expired share links for user {UserId}", expired.Count, userId);
            }
        }
    }

    public class ShareLinkDto
    {
        public string AccessKey { get; set; } = string.Empty;
        public string TargetPath { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public bool IsExpired { get; set; }
    }

    public class ShareLinkValidation
    {
        public bool IsValid { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string AccessKey { get; set; } = string.Empty;
        public string TargetPath { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public DateTime? ExpiredAt { get; set; }
    }
}


