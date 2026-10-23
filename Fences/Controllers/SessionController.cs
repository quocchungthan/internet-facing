using Fences.Models;
using Fences.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace Fences.Controllers;

[ApiController]
public sealed class SessionController(IAppCatalog appCatalog, IOptionsMonitor<IdentityAppOptions> options) : ControllerBase
{
    [HttpGet("/api/session")]
    public async Task<IActionResult> GetSession()
    {
        Response.Headers.CacheControl = "no-store";
        var brandName = options.CurrentValue.BrandName;
        if (User.Identity?.IsAuthenticated != true)
        {
            return Ok(new { isAuthenticated = false, brandName, loginUrl = "/auth/login", apps = Array.Empty<object>() });
        }

        var accessToken = await HttpContext.GetTokenAsync("access_token");
        return Ok(new
        {
            isAuthenticated = true,
            brandName,
            name = User.FindFirstValue(ClaimTypes.Name),
            email = User.FindFirstValue(ClaimTypes.Email),
            githubLogin = User.FindFirstValue("urn:github:login"),
            avatarUrl = User.FindFirstValue("urn:github:avatar"),
            hasGithubToken = !string.IsNullOrWhiteSpace(accessToken),
            apps = appCatalog.GetApps()
        });
    }

    [HttpGet("/api/apps")]
    public IActionResult GetApps() => Ok(appCatalog.GetApps());
}
