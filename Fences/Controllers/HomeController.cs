using Fences.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fences.Controllers;

public sealed class HomeController(IWebHostEnvironment environment, ReturnUrlPolicy returnUrlPolicy) : Controller
{
    private IActionResult Frontend()
    {
        var entry = Path.Combine(environment.ContentRootPath, "frontend", "index.html");
        if (!System.IO.File.Exists(entry))
        {
            return StatusCode(503, new { error = "Identity frontend is not installed. Import the pinned app-identity artifact." });
        }
        Response.Headers.CacheControl = "no-store";
        return PhysicalFile(entry, "text/html; charset=utf-8");
    }

    [HttpGet("/")]
    public IActionResult Index([FromQuery] string? callbackUrl, [FromQuery] string? returnUrl)
    {
        var effectiveReturnUrl = returnUrl ?? callbackUrl;
        if (User.Identity?.IsAuthenticated == true && string.IsNullOrWhiteSpace(effectiveReturnUrl))
        {
            return Redirect(returnUrlPolicy.ResolveSafeReturnUrl(effectiveReturnUrl));
        }

        return Frontend();
    }

    [Authorize]
    [HttpGet("/apps")]
    public IActionResult Apps() => Frontend();
}
