using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
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
    public async Task<IActionResult> Authorize()
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
        var emailVerifiedClaim = User.FindFirstValue("urn:github:email_verified");
        if (string.IsNullOrWhiteSpace(email))
        {
            var verifiedEmail = await GetVerifiedGitHubEmailAsync(HttpContext, await HttpContext.GetTokenAsync("access_token"));
            if (!string.IsNullOrWhiteSpace(verifiedEmail))
            {
                email = verifiedEmail;
                emailVerifiedClaim = "true";
            }
        }

        var emailVerified = !string.Equals(emailVerifiedClaim, bool.FalseString, StringComparison.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(email))
        {
            identity.SetClaim(OpenIddictConstants.Claims.Email, email);
            identity.SetClaim(OpenIddictConstants.Claims.EmailVerified, emailVerified ? "true" : "false");
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
        if (!string.IsNullOrWhiteSpace(email))
        {
            claims[OpenIddictConstants.Claims.Email] = email;
            claims[OpenIddictConstants.Claims.EmailVerified] = !string.Equals(User.FindFirstValue(OpenIddictConstants.Claims.EmailVerified), bool.FalseString, StringComparison.OrdinalIgnoreCase);
        }

        return Ok(claims);
    }

    private static async Task<string?> GetVerifiedGitHubEmailAsync(HttpContext httpContext, string? accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return null;
        }

        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user/emails");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.UserAgent.ParseAdd("fences-identity-app");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var client = httpContext.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient();
        var response = await client.SendAsync(request, httpContext.RequestAborted);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(httpContext.RequestAborted));
        return payload.RootElement.EnumerateArray()
            .Select(email => new
            {
                Address = email.TryGetProperty("email", out var address) ? address.GetString() : null,
                Primary = email.TryGetProperty("primary", out var primary) && primary.GetBoolean(),
                Verified = email.TryGetProperty("verified", out var verified) && verified.GetBoolean()
            })
            .Where(email => email.Verified && !string.IsNullOrWhiteSpace(email.Address))
            .OrderByDescending(email => email.Primary)
            .Select(email => email.Address)
            .FirstOrDefault();
    }
}