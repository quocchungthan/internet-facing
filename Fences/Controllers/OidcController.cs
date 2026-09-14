using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;

namespace Fences.Controllers;

public sealed class OidcController : Controller
{
    [HttpGet("/connect/authorize")]
    [HttpPost("/connect/authorize")]
    [IgnoreAntiforgeryToken]
    public IActionResult Authorize()
    {
        var request = Microsoft.AspNetCore.OpenIddictServerAspNetCoreHelpers.GetOpenIddictServerRequest(HttpContext)
            ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        if (User.Identity?.IsAuthenticated != true)
        {
            var returnUrl = Request.PathBase + Request.Path + Request.QueryString;
            return Challenge(new AuthenticationProperties { RedirectUri = returnUrl }, "GitHub");
        }

        var githubId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(githubId))
        {
            return Forbid("GitHub");
        }

        var subject = $"github:{githubId}";
        var identity = new ClaimsIdentity(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            OpenIddictConstants.Claims.Name,
            OpenIddictConstants.Claims.Role);
        identity.SetClaim(OpenIddictConstants.Claims.Subject, subject);
        identity.SetClaim(OpenIddictConstants.Claims.Name, User.FindFirstValue(ClaimTypes.Name) ?? githubId);

        var email = User.FindFirstValue(ClaimTypes.Email);
        if (!string.IsNullOrWhiteSpace(email))
        {
            identity.SetClaim(OpenIddictConstants.Claims.Email, email);
        }

        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(request.GetScopes());
        principal.SetResources("fences");
        principal.SetDestinations(static claim => claim.Type switch
        {
            OpenIddictConstants.Claims.Name or OpenIddictConstants.Claims.Email =>
                [OpenIddictConstants.Destinations.IdentityToken, OpenIddictConstants.Destinations.AccessToken],
            _ => [OpenIddictConstants.Destinations.AccessToken]
        });

        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }
}