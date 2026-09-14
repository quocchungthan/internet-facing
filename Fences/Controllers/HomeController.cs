using Fences.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fences.Controllers;

public sealed class HomeController(IAppCatalog appCatalog, ReturnUrlPolicy returnUrlPolicy) : Controller
{
    [HttpGet("/")]
    public IActionResult Index([FromQuery] string? callbackUrl, [FromQuery] string? returnUrl)
    {
        var effectiveReturnUrl = returnUrl ?? callbackUrl;
        if (User.Identity?.IsAuthenticated == true && string.IsNullOrWhiteSpace(effectiveReturnUrl))
        {
            return Redirect(returnUrlPolicy.ResolveSafeReturnUrl(effectiveReturnUrl));
        }

        ViewData["ReturnUrl"] = effectiveReturnUrl;
        return View(appCatalog.GetApps());
    }

    [Authorize]
    [HttpGet("/apps")]
    public IActionResult Apps() => View("Index", appCatalog.GetApps());
}
