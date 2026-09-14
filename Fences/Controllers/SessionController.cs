using Fences.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Fences.Controllers;

[ApiController]
public sealed class SessionController(IAppCatalog appCatalog) : ControllerBase
{
    [HttpGet("/api/session")]
    public async Task<IActionResult> GetSession()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Ok(new { isAuthenticated = false, loginUrl = "/auth/login", apps = Array.Empty<object>() });
        }

        var accessToken = await HttpContext.GetTokenAsync("access_token");
        return Ok(new
        {
            isAuthenticated = true,
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
