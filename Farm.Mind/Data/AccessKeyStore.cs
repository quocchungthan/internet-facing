using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Farm.Auth;
using Microsoft.EntityFrameworkCore;

namespace Farm.Mind.Data;

public record AccessKeyDto(Guid Id, string Name, string Preview, DateTimeOffset CreatedAt, DateTimeOffset? LastUsedAt);
public record CreatedAccessKey(AccessKeyDto Key, string Secret);

public class AccessKeyStore(MindDbContext db) : IAccessKeyValidator
{
    public const int MaxKeysPerUser = 20;

    private static string Hash(string key) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();

    private static AccessKeyDto ToDto(AccessKey k) => new(k.Id, k.Name, k.Preview, k.CreatedAt, k.LastUsedAt);

    public async Task<CreatedAccessKey> CreateAsync(string subject, string name, CancellationToken ct = default)
    {
        name = name.Trim();
        if (name.Length is 0 or > 100) throw new ArgumentException("Key name must be 1-100 characters.");
        if (await db.AccessKeys.CountAsync(k => k.Subject == subject, ct) >= MaxKeysPerUser)
            throw new ArgumentException($"At most {MaxKeysPerUser} keys per account; revoke one first.");

        var secret = IAccessKeyValidator.Prefix + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var key = new AccessKey
        {
            Id = Guid.NewGuid(),
            Subject = subject,
            Name = name,
            Hash = Hash(secret),
            Preview = secret[..8] + "…",
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.AccessKeys.Add(key);
        await db.SaveChangesAsync(ct);
        return new CreatedAccessKey(ToDto(key), secret);
    }

    public async Task<IReadOnlyList<AccessKeyDto>> ListAsync(string subject, CancellationToken ct = default) =>
        (await db.AccessKeys.AsNoTracking().Where(k => k.Subject == subject).OrderByDescending(k => k.CreatedAt).ToListAsync(ct))
        .Select(ToDto).ToList();

    public async Task<bool> RevokeAsync(string subject, Guid id, CancellationToken ct = default)
    {
        var key = await db.AccessKeys.FirstOrDefaultAsync(k => k.Id == id && k.Subject == subject, ct);
        if (key is null) return false;
        db.AccessKeys.Remove(key);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<ClaimsPrincipal?> ValidateAsync(string key, CancellationToken ct)
    {
        var hash = Hash(key);
        var found = await db.AccessKeys.FirstOrDefaultAsync(k => k.Hash == hash, ct);
        if (found is null) return null;

        if (found.LastUsedAt is null || DateTimeOffset.UtcNow - found.LastUsedAt > TimeSpan.FromMinutes(10))
        {
            found.LastUsedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        var identity = new ClaimsIdentity([new Claim("sub", found.Subject), new Claim("name", found.Name)], "AccessKey", "name", null);
        return new ClaimsPrincipal(identity);
    }
}
