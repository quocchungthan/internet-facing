using Fences.Models;
using Fences.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Fences.Controllers;

public sealed class AuthController(ReturnUrlPolicy returnUrlPolicy, IOptions<IdentityAppOptions> identityOptions) : Controller
{
    private const string GitHubScheme = "GitHub";
    private readonly int persistentLoginDays = identityOptions.Value.PersistentLoginDays > 0 ? identityOptions.Value.PersistentLoginDays : 30;

    [HttpGet("/auth/login")]
    public IActionResult Login([FromQuery] string? callbackUrl, [FromQuery] string? returnUrl)
    {
        var effectiveReturnUrl = returnUrl ?? callbackUrl;
        if (User.Identity?.IsAuthenticated == true)
        {
            return Redirect(returnUrlPolicy.ResolveSafeReturnUrl(effectiveReturnUrl));
        }

        var redirectUri = Url.Action(nameof(PostLogin), values: new { returnUrl = effectiveReturnUrl }) ?? "/";
        return Challenge(new AuthenticationProperties
        {
            RedirectUri = redirectUri,
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(persistentLoginDays),
            AllowRefresh = true,
        }, [GitHubScheme]);
    }

    [HttpGet("/auth/post-login")]
    public IActionResult PostLogin([FromQuery] string? callbackUrl, [FromQuery] string? returnUrl) => Redirect(returnUrlPolicy.ResolveSafeReturnUrl(returnUrl ?? callbackUrl));

    [HttpGet("/auth/logout")]
    public async Task<IActionResult> Logout([FromQuery] string? callbackUrl, [FromQuery] string? returnUrl)
    {
        await HttpContext.SignOutAsync("IdentityCookies");
        return Redirect(returnUrlPolicy.ResolveSafeReturnUrl(returnUrl ?? callbackUrl));
    }
}
