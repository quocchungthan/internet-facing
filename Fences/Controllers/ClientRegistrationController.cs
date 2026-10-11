using System.Security.Cryptography;
using Fences.Models;
using Fences.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OpenIddict.Abstractions;

namespace Fences.Controllers;

/// <summary>RFC 7591 dynamic client registration, so MCP clients can onboard themselves.</summary>
public sealed class ClientRegistrationController(IOpenIddictApplicationManager applications, IdentityAppOptions options) : Controller
{
    [HttpPost("/connect/register")]
    [EnableRateLimiting("client-registration")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Register([FromBody] ClientRegistrationRequest request, CancellationToken cancellationToken)
    {
        if (!options.AllowDynamicClientRegistration) return NotFound();

        var schemes = DynamicClientRegistration.DefaultCustomSchemes.Concat(options.DynamicClientRedirectSchemes).ToArray();
        var hosts = DynamicClientRegistration.DefaultHttpsHosts.Concat(options.DynamicClientRedirectHosts).ToArray();
        var result = DynamicClientRegistration.Validate(request, schemes, hosts);
        if (!result.IsValid)
            return BadRequest(new { error = result.Error, error_description = result.Description });
        if (await applications.CountAsync(cancellationToken) >= options.MaxDynamicClients)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "temporarily_unavailable", error_description = "Client registration is full." });

        var clientId = "dcr-" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            DisplayName = string.IsNullOrWhiteSpace(request.ClientName) ? "MCP client" : request.ClientName.Trim(),
            ClientType = OpenIddictConstants.ClientTypes.Public,
            ConsentType = OpenIddictConstants.ConsentTypes.Implicit,
            Permissions =
            {
                OpenIddictConstants.Permissions.Endpoints.Authorization,
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
                OpenIddictConstants.Permissions.ResponseTypes.Code,
                OpenIddictConstants.Permissions.Scopes.Profile,
                OpenIddictConstants.Permissions.Scopes.Email,
                OpenIddictConstants.Permissions.Prefixes.Scope + OpenIddictConstants.Scopes.OfflineAccess,
                "rsrc:" + options.McpAudience
            },
            Requirements = { OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange }
        };
        descriptor.RedirectUris.UnionWith(result.RedirectUris);
        await applications.CreateAsync(descriptor, cancellationToken);

        return StatusCode(StatusCodes.Status201Created, new
        {
            client_id = clientId,
            client_id_issued_at = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            client_name = descriptor.DisplayName,
            redirect_uris = result.RedirectUris.Select(uri => uri.OriginalString),
            token_endpoint_auth_method = "none",
            grant_types = new[] { "authorization_code", "refresh_token" },
            response_types = new[] { "code" }
        });
    }
}
