using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Farm.Models;

namespace Farm.Controllers;

public class HomeController(IWebHostEnvironment environment) : Controller
{
    private IActionResult Frontend()
    {
        var entry = Path.Combine(environment.ContentRootPath, "frontend", "index.html");
        if (!System.IO.File.Exists(entry))
        {
            return StatusCode(503, new { error = "Farm frontend is not installed. Import the pinned app-farm artifact." });
        }
        Response.Headers.CacheControl = "no-store";
        return PhysicalFile(entry, "text/html; charset=utf-8");
    }

    public IActionResult Index()
    {
        return Frontend();
    }

    [HttpGet("winchester")]
    public IActionResult Winchester()
    {
        return Frontend();
    }

    [HttpGet("connect")]
    public IActionResult Connect()
    {
        return Frontend();
    }

    [HttpGet("tracker")]
    [HttpGet("brick")]
    [HttpGet("bricks")]
    public IActionResult Tracker()
    {
        return Frontend();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
