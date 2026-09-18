using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using OpenIddict.Validation.AspNetCore;

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

        var githubLogin = User.FindFirstValue("urn:github:login");
        if (!string.IsNullOrWhiteSpace(githubLogin))
        {
            identity.SetClaim(OpenIddictConstants.Claims.PreferredUsername, githubLogin);
        }

        var avatarUrl = User.FindFirstValue("urn:github:avatar");
        if (!string.IsNullOrWhiteSpace(avatarUrl))
        {
            identity.SetClaim(OpenIddictConstants.Claims.Picture, avatarUrl);
        }

        var email = User.FindFirstValue(ClaimTypes.Email);
        var emailVerified = string.Equals(User.FindFirstValue("urn:github:email_verified"), bool.TrueString, StringComparison.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(email) && emailVerified)
        {
            identity.SetClaim(OpenIddictConstants.Claims.Email, email);
            identity.SetClaim(OpenIddictConstants.Claims.EmailVerified, bool.TrueString);
        }

        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(request.GetScopes());
        principal.SetResources("fences");
        principal.SetDestinations(static claim => claim.Type switch
        {
            OpenIddictConstants.Claims.Name
                or OpenIddictConstants.Claims.PreferredUsername
                or OpenIddictConstants.Claims.Picture
                or OpenIddictConstants.Claims.Email
                or OpenIddictConstants.Claims.EmailVerified =>
                [OpenIddictConstants.Destinations.IdentityToken, OpenIddictConstants.Destinations.AccessToken],
            _ => [OpenIddictConstants.Destinations.AccessToken]
        });

        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
    [HttpGet("/connect/userinfo")]
    [HttpPost("/connect/userinfo")]
    public IActionResult Userinfo()
    {
        var subject = User.FindFirstValue(OpenIddictConstants.Claims.Subject);
        if (string.IsNullOrWhiteSpace(subject))
        {
            return Forbid(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
        }

        var claims = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [OpenIddictConstants.Claims.Subject] = subject
        };

        var name = User.FindFirstValue(OpenIddictConstants.Claims.Name);
        if (!string.IsNullOrWhiteSpace(name))
        {
            claims[OpenIddictConstants.Claims.Name] = name;
        }

        var preferredUsername = User.FindFirstValue(OpenIddictConstants.Claims.PreferredUsername);
        if (!string.IsNullOrWhiteSpace(preferredUsername))
        {
            claims[OpenIddictConstants.Claims.PreferredUsername] = preferredUsername;
        }

        var picture = User.FindFirstValue(OpenIddictConstants.Claims.Picture);
        if (!string.IsNullOrWhiteSpace(picture))
        {
            claims[OpenIddictConstants.Claims.Picture] = picture;
        }

        var email = User.FindFirstValue(OpenIddictConstants.Claims.Email);
        var emailVerified = string.Equals(User.FindFirstValue(OpenIddictConstants.Claims.EmailVerified), bool.TrueString, StringComparison.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(email) && emailVerified)
        {
            claims[OpenIddictConstants.Claims.Email] = email;
            claims[OpenIddictConstants.Claims.EmailVerified] = true;
        }

        return Ok(claims);
    }
}