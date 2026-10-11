namespace Farm.Auth;

/// <summary>
/// Settings shared by every Farm service that signs users in through Fences (the identity service).
/// Bound from the <c>FarmAuth</c> configuration section.
/// </summary>
public sealed class FarmAuthOptions
{
    public const string SectionName = "FarmAuth";

    /// <summary>Fences issuer, for example https://identity.eldervibe.dev. Blank turns sign-in off.</summary>
    public string? Authority { get; set; }

    /// <summary>OIDC client id registered in Fences (IdentityApp:OidcClients) for the Farm UI.</summary>
    public string? ClientId { get; set; }

    public string? ClientSecret { get; set; }

    /// <summary>
    /// Audience (RFC 8707 resource URI) access tokens must carry when an MCP/API client calls Farm.Mind.
    /// Defaults to the public MCP URL, which is also what MCP clients send as the <c>resource</c> parameter.
    /// </summary>
    public string? Audience { get; set; }

    public string ResolvedAudience => string.IsNullOrWhiteSpace(Audience) ? PublicBaseUrl.TrimEnd('/') + "/mcp" : Audience;

    /// <summary>Public origin of the main domain; used for return URLs and resource metadata.</summary>
    public string PublicBaseUrl { get; set; } = "https://eldervibe.dev";

    /// <summary>Must be identical in every Farm service so they all accept the same session cookie.</summary>
    public string CookieName { get; set; } = "eldervibe.farm";

    public string? CookieDomain { get; set; }

    public string ApplicationName { get; set; } = "eldervibe-farm";

    /// <summary>Shared Data Protection key ring directory (mount the same volume in each service).</summary>
    public string? DataProtectionKeysPath { get; set; }

    public bool Enabled => !string.IsNullOrWhiteSpace(Authority) && !string.IsNullOrWhiteSpace(ClientId);

    /// <summary>Authority without a trailing slash.</summary>
    public string NormalizedAuthority => (Authority ?? string.Empty).TrimEnd('/');
}
