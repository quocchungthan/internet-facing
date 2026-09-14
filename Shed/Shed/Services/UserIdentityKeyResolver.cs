using System.Security.Claims;

namespace Shed.Services;

public static class UserIdentityKeyResolver
{
    public static string Resolve(ClaimsPrincipal? user)
    {
        var candidates = ResolveCandidates(user);
        return candidates.FirstOrDefault() ?? "user";
    }

    public static IReadOnlyList<string> ResolveCandidates(ClaimsPrincipal? user)
    {
        var candidates = new List<string>();

        AddCandidate(candidates, user?.FindFirstValue("urn:identity:key"));
        AddCandidate(candidates, user?.FindFirstValue("urn:github:login"));
        AddCandidate(candidates, user?.FindFirstValue(ClaimTypes.Email)?.Trim().ToLowerInvariant());
        AddCandidate(candidates, user?.FindFirstValue(ClaimTypes.NameIdentifier));
        AddCandidate(candidates, user?.Identity?.Name);

        return candidates;
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
}


