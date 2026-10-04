using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Farm.Models;

namespace Farm.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet("winchester")]
    public IActionResult Winchester()
    {
        return View("Index");
    }

    [HttpGet("tracker")]
    [HttpGet("brick")]
    [HttpGet("bricks")]
    public IActionResult Tracker()
    {
        return View("Index");
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
