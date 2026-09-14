using Fences.Models;
using Microsoft.Extensions.Options;

namespace Fences.Services;

public sealed class ReturnUrlPolicy(IOptionsMonitor<IdentityAppOptions> optionsMonitor)
{
    private const string DefaultMainDomainUrl = "https://shuneo.com";

    public string ResolveSafeReturnUrl(string? returnUrl)
    {
        var configuredFallback = optionsMonitor.CurrentValue.DefaultReturnUrl;
        var defaultMainDomainUrl = string.IsNullOrWhiteSpace(configuredFallback)
            ? DefaultMainDomainUrl
            : configuredFallback;

        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return defaultMainDomainUrl;
        }

        if (Uri.TryCreate(returnUrl, UriKind.Relative, out var relativeUri))
        {
            var text = relativeUri.ToString();
            if (text.StartsWith("/"))
            {
                return text;
            }
        }

        if (!Uri.TryCreate(returnUrl, UriKind.Absolute, out var absoluteUri))
        {
            return "/";
        }

        if (!IsAllowedScheme(absoluteUri))
        {
            return "/";
        }

        var targetHost = absoluteUri.IsDefaultPort
            ? absoluteUri.Host
            : $"{absoluteUri.Host}:{absoluteUri.Port}";

        var allowedHosts = optionsMonitor.CurrentValue.AllowedReturnHosts;
        var isAllowedHost = allowedHosts.Any(host => string.Equals(host, targetHost, StringComparison.OrdinalIgnoreCase));

        return isAllowedHost ? absoluteUri.ToString() : "/";
    }

    private static bool IsAllowedScheme(Uri uri)
    {
        if (uri.Scheme == Uri.UriSchemeHttps)
        {
            return true;
        }

        return uri.Scheme == Uri.UriSchemeHttp
            && (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                || uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase));
    }
}
