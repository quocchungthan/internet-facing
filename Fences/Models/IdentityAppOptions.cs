namespace Fences.Models;

public sealed class IdentityAppOptions
{
    public string SharedThemeBaseUrl { get; init; } = "https://eldervibe.dev/api/styles";
    public string DefaultReturnUrl { get; init; } = "https://shuneo.com";
    public string BrandName { get; init; } = "Shuneo";
    public string? CookieDomain { get; init; }
    public string DataProtectionKeysPath { get; init; } = "App_Data/keys";
    public string IdentityDatabasePath { get; init; } = "App_Data/identity.db";
    public string OidcIssuer { get; init; } = "https://identity.eldervibe.dev";
    public string OidcSigningCertificatePath { get; init; } = string.Empty;
    public string OidcSigningCertificatePassword { get; init; } = string.Empty;
    public string OidcEncryptionCertificatePath { get; init; } = string.Empty;
    public string OidcEncryptionCertificatePassword { get; init; } = string.Empty;
    public List<OidcClientOptions> OidcClients { get; init; } = [];
    public int PersistentLoginDays { get; init; } = 30;
    public List<string> AllowedReturnHosts { get; init; } = [];
    public List<string> AllowedCorsOrigins { get; init; } = [];
    public List<IdentityNavigationApp> Apps { get; init; } = [];
}

public sealed class OidcClientOptions
{
    public string ClientId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public List<string> RedirectUris { get; init; } = [];
    public List<string> PostLogoutRedirectUris { get; init; } = [];
}

public sealed class IdentityNavigationApp
{
    public string Key { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}
