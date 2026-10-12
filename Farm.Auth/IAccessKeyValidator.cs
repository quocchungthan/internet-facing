using System.Security.Claims;

namespace Farm.Auth;

/// <summary>
/// Optional hook: lets a service accept its own long-lived access keys (prefix <see cref="Prefix"/>)
/// as bearer tokens, for clients that cannot do the browser sign-in.
/// </summary>
public interface IAccessKeyValidator
{
    public const string Prefix = "fmk_";

    Task<ClaimsPrincipal?> ValidateAsync(string key, CancellationToken cancellationToken);
}
