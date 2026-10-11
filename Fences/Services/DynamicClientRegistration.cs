using System.Text.Json.Serialization;

namespace Fences.Services;

/// <summary>RFC 7591 request body, reduced to what public MCP clients send.</summary>
public sealed class ClientRegistrationRequest
{
    [JsonPropertyName("client_name")] public string? ClientName { get; init; }
    [JsonPropertyName("redirect_uris")] public List<string>? RedirectUris { get; init; }
    [JsonPropertyName("token_endpoint_auth_method")] public string? TokenEndpointAuthMethod { get; init; }
    [JsonPropertyName("grant_types")] public List<string>? GrantTypes { get; init; }
    [JsonPropertyName("response_types")] public List<string>? ResponseTypes { get; init; }
    [JsonPropertyName("scope")] public string? Scope { get; init; }
}

public sealed record ClientRegistrationResult(bool IsValid, string? Error, string? Description, IReadOnlyList<Uri> RedirectUris)
{
    public static ClientRegistrationResult Invalid(string error, string description) => new(false, error, description, []);
}

/// <summary>
/// Policy for self-registered clients. Only public, PKCE-protected authorization-code clients may register,
/// and only with redirect URIs that cannot send the code to an arbitrary web host.
/// </summary>
public static class DynamicClientRegistration
{
    public static readonly string[] DefaultCustomSchemes = ["vscode", "vscode-insiders", "cursor"];
    public static readonly string[] DefaultHttpsHosts = ["vscode.dev", "insiders.vscode.dev", "claude.ai"];

    public static ClientRegistrationResult Validate(
        ClientRegistrationRequest request, IReadOnlyCollection<string> customSchemes, IReadOnlyCollection<string> httpsHosts)
    {
        if (request.TokenEndpointAuthMethod is not (null or "none"))
            return ClientRegistrationResult.Invalid("invalid_client_metadata", "Only public clients (token_endpoint_auth_method=none) can register.");
        if (request.GrantTypes is { } grants && grants.Any(grant => grant is not ("authorization_code" or "refresh_token")))
            return ClientRegistrationResult.Invalid("invalid_client_metadata", "Only authorization_code and refresh_token grants are supported.");
        if (request.ResponseTypes is { } responses && responses.Any(response => response != "code"))
            return ClientRegistrationResult.Invalid("invalid_client_metadata", "Only the code response type is supported.");
        if (request.ClientName is { Length: > 100 })
            return ClientRegistrationResult.Invalid("invalid_client_metadata", "client_name is too long.");
        if (request.RedirectUris is not { Count: > 0 and <= 10 })
            return ClientRegistrationResult.Invalid("invalid_redirect_uri", "Between 1 and 10 redirect_uris are required.");

        var uris = new List<Uri>();
        foreach (var value in request.RedirectUris)
        {
            if (value.Length > 512 || !Uri.TryCreate(value, UriKind.Absolute, out var uri) || !string.IsNullOrEmpty(uri.Fragment))
                return ClientRegistrationResult.Invalid("invalid_redirect_uri", "Redirect URIs must be absolute and have no fragment.");
            if (!IsAllowed(uri, customSchemes, httpsHosts))
                return ClientRegistrationResult.Invalid("invalid_redirect_uri", $"Redirect URI is not allowed: {uri.Scheme}://{uri.Host}.");
            uris.Add(uri);
        }

        return new(true, null, null, uris);
    }

    private static bool IsAllowed(Uri uri, IReadOnlyCollection<string> customSchemes, IReadOnlyCollection<string> httpsHosts)
    {
        if (uri.Scheme == Uri.UriSchemeHttp)
            return uri.IsLoopback && string.IsNullOrEmpty(uri.UserInfo);
        if (uri.Scheme == Uri.UriSchemeHttps)
            return string.IsNullOrEmpty(uri.UserInfo) && httpsHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase);
        return customSchemes.Contains(uri.Scheme, StringComparer.OrdinalIgnoreCase);
    }
}
