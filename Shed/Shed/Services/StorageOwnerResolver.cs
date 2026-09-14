using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Shed.Data;

namespace Shed.Services;

public sealed record StorageOwnerResolution(string UserId, IReadOnlyCollection<string> CandidateUserIds);

public interface IStorageOwnerResolver
{
    Task<StorageOwnerResolution> ResolveAsync(ClaimsPrincipal? user, CancellationToken cancellationToken);
}

public sealed class StorageOwnerResolver(
    IConfiguration configuration,
    ApplicationDbContext applicationDbContext)
    : IStorageOwnerResolver
{
    public async Task<StorageOwnerResolution> ResolveAsync(ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        var candidates = new List<string>(UserIdentityKeyResolver.ResolveCandidates(user));

        var email = user?.FindFirstValue(ClaimTypes.Email)?.Trim();
        if (!string.IsNullOrWhiteSpace(email))
        {
            var normalizedEmail = email.ToUpperInvariant();
            var localUserIdsByEmail = await applicationDbContext.Users
                .AsNoTracking()
                .Where(x => x.NormalizedEmail == normalizedEmail)
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);

            foreach (var localUserId in localUserIdsByEmail)
            {
                AddCandidate(candidates, localUserId);
            }
        }

        var githubLogin = user?.FindFirstValue("urn:github:login")?.Trim();
        if (!string.IsNullOrWhiteSpace(githubLogin))
        {
            var normalizedUserName = githubLogin.ToUpperInvariant();
            var localUserIdsByUserName = await applicationDbContext.Users
                .AsNoTracking()
                .Where(x => x.NormalizedUserName == normalizedUserName)
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);

            foreach (var localUserId in localUserIdsByUserName)
            {
                AddCandidate(candidates, localUserId);
            }
        }

        if (candidates.Count == 0)
        {
            candidates.Add("user");
        }

        var storageRoot = configuration["Storage:RootPath"]
            ?? @"/app/data/prodstorage";

        var selected = candidates.FirstOrDefault(candidate =>
            Directory.Exists(Path.Combine(storageRoot, SanitizeFolderSegment(candidate))));

        return new StorageOwnerResolution(selected ?? candidates[0], candidates);
    }

    private static void AddCandidate(List<string> candidates, string? value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        if (!candidates.Contains(normalized, StringComparer.OrdinalIgnoreCase))
        {
            candidates.Add(normalized);
        }
    }

    private static string SanitizeFolderSegment(string value)
    {
        var cleaned = value;
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            cleaned = cleaned.Replace(invalid, '_');
        }

        cleaned = cleaned.Replace("/", "_").Replace("\\", "_");
        return string.IsNullOrWhiteSpace(cleaned) ? "user" : cleaned;
    }
}


