using Microsoft.EntityFrameworkCore;
using OpensourceLab.FileStorage.ServedServices;
using Shed.Data;

namespace Shed.Services
{
    public interface IAccessKeyValidator
    {
        Task<AccessKeyValidationResult> ValidateAsync(string? accessKey, string? requestedPath, CancellationToken cancellationToken);
    }

    public record AccessKeyValidationResult(
        bool IsValid,
        string UserId,
        IReadOnlyList<string> AllowedPathWildcards,
        bool CanRead,
        bool CanWrite,
        string Remark,
        string Error
    );

    public class AccessKeyValidator : IAccessKeyValidator
    {
        private readonly VanillaDbContext _dbContext;

        public AccessKeyValidator(VanillaDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<AccessKeyValidationResult> ValidateAsync(string? accessKey, string? requestedPath, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(accessKey))
            {
                return new AccessKeyValidationResult(false, string.Empty, Array.Empty<string>(), false, false, string.Empty, "Missing access key.");
            }

            var key = await _dbContext.AccessKeys
                .AsNoTracking()
                .Where(x => x.Key == accessKey.Trim())
                .Select(x => new
                {
                    x.UserId,
                    x.FilePathWildCards,
                    CanRead = EF.Property<bool>(x, "CanRead"),
                    CanWrite = EF.Property<bool>(x, "CanWrite"),
                    Remark = EF.Property<string>(x, "Remark")
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (key is null)
            {
                return new AccessKeyValidationResult(false, string.Empty, Array.Empty<string>(), false, false, string.Empty, "Access key not found.");
            }

            var scope = PathWildcardScope.FromDelimited(key.FilePathWildCards);
            var path = NormalizePath(requestedPath);
            if (!string.IsNullOrWhiteSpace(path) && !scope.IsAllowed(path))
            {
                return new AccessKeyValidationResult(false, string.Empty, scope.Wildcards, key.CanRead, key.CanWrite, key.Remark ?? string.Empty, "Path is not allowed for this access key.");
            }

            return new AccessKeyValidationResult(true, key.UserId, scope.Wildcards, key.CanRead, key.CanWrite, key.Remark ?? string.Empty, string.Empty);
        }

        private static string NormalizePath(string? raw)
        {
            return PathWildcardScope.NormalizePath(raw ?? string.Empty);
        }
    }
}


