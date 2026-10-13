using Farm.Data;
using Farm.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Farm.Controllers;

[ApiController]
[Route("api")]
public class ApiController : ControllerBase
{
    private readonly AuraFarming _db;
    private readonly IClientTracingService _tracingService;
    private readonly ILogger<ApiController> _logger;

    public ApiController(
        AuraFarming db,
        IClientTracingService tracingService,
        ILogger<ApiController> logger)
    {
        _db = db;
        _tracingService = tracingService;
        _logger = logger;
    }

    [HttpGet("metadata")]
    public async Task<IActionResult> GetMetadata(CancellationToken cancellationToken)
    {
        const string endpoint = "/api/metadata";
        var clientResult = _tracingService.ProcessRequest(HttpContext, endpoint);

        if (!clientResult.IsAllowed)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new
            {
                error = "Too Many Requests",
                message = $"Rate limit of {clientResult.Limit} requests exceeded. Please retry in {clientResult.ResetSeconds} seconds.",
                rateLimit = clientResult
            });
        }

        var metadata = await _db.WebsiteMetadata
            .AsNoTracking()
            .OrderBy(m => m.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (metadata == null)
        {
            return NotFound(new { error = "Website metadata not configured." });
        }

        return Ok(new
        {
            developerName = metadata.DeveloperName,
            title = metadata.Title,
            description = metadata.Description,
            repositoryUrl = metadata.RepositoryUrl,
            specifiedIssueUrl = metadata.SpecifiedIssueUrl
        });
    }

    [HttpGet("feeds")]
    public async Task<IActionResult> GetFeeds(CancellationToken cancellationToken)
    {
        const string endpoint = "/api/feeds";
        var clientResult = _tracingService.ProcessRequest(HttpContext, endpoint);

        if (!clientResult.IsAllowed)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new
            {
                error = "Too Many Requests",
                message = $"Rate limit of {clientResult.Limit} requests exceeded. Please retry in {clientResult.ResetSeconds} seconds.",
                rateLimit = clientResult
            });
        }

        var bookmarks = await _db.PlatformBookmarks
            .AsNoTracking()
            .Where(b => b.IsActive)
            .OrderBy(b => b.DisplayOrder)
            .ThenBy(b => b.Id)
            .Select(b => new
            {
                id = b.Id,
                platform = b.Platform,
                url = b.Url,
                platformId = b.PlatformId,
                description = b.Description,
                color = b.Color,
                displayOrder = b.DisplayOrder
            })
            .ToListAsync(cancellationToken);

        return Ok(bookmarks);
    }

    [HttpGet("tools")]
    public async Task<IActionResult> GetTools(CancellationToken cancellationToken)
    {
        const string endpoint = "/api/tools";
        var clientResult = _tracingService.ProcessRequest(HttpContext, endpoint);

        if (!clientResult.IsAllowed)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new
            {
                error = "Too Many Requests",
                message = $"Rate limit of {clientResult.Limit} requests exceeded. Please retry in {clientResult.ResetSeconds} seconds.",
                rateLimit = clientResult
            });
        }

        var tools = await _db.LocalhostTools
            .AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.Category)
            .ThenBy(t => t.DisplayOrder)
            .ThenBy(t => t.Id)
            .Select(t => new
            {
                id = t.Id,
                name = t.Name,
                description = t.Description,
                category = t.Category,
                directUrl = t.DirectUrl,
                repositoryUrl = t.RepositoryUrl,
                issueUrl = t.IssueUrl,
                displayOrder = t.DisplayOrder
            })
            .ToListAsync(cancellationToken);

        return Ok(tools);
    }

    [HttpGet("feeds/clicks")]
    public async Task<IActionResult> GetClicks([FromServices] LinkClickDbContext clicks, CancellationToken cancellationToken)
    {
        var from = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-(24 * 7 - 1));
        var rows = await clicks.LinkClicks
            .AsNoTracking()
            .Where(c => c.Day >= from)
            .Select(c => new { platform = c.Platform, day = c.Day, count = c.Count })
            .ToListAsync(cancellationToken);
        return Ok(rows);
    }

    [HttpPost("feeds/{platform}/click")]
    public async Task<IActionResult> RegisterClick(string platform, [FromServices] LinkClickDbContext clicks, CancellationToken cancellationToken)
    {
        var clientResult = _tracingService.ProcessRequest(HttpContext, "/api/feeds/click");
        if (!clientResult.IsAllowed)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new { error = "Too Many Requests" });
        }

        // Only known, active platforms can be counted.
        var known = await _db.PlatformBookmarks
            .AsNoTracking()
            .Where(b => b.IsActive)
            .Select(b => b.Platform)
            .ToListAsync(cancellationToken);
        var name = known.FirstOrDefault(p => string.Equals(p, platform, StringComparison.OrdinalIgnoreCase));
        if (name is null) return NotFound();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var row = await clicks.LinkClicks.FirstOrDefaultAsync(c => c.Platform == name && c.Day == today, cancellationToken);
        if (row is null)
        {
            clicks.LinkClicks.Add(new Farm.Data.Entities.LinkClickDaily { Platform = name, Day = today, Count = 1 });
        }
        else
        {
            row.Count++;
        }

        try
        {
            await clicks.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Lost a race creating today's row; retry as an increment.
            clicks.ChangeTracker.Clear();
            var existing = await clicks.LinkClicks.FirstAsync(c => c.Platform == name && c.Day == today, cancellationToken);
            existing.Count++;
            await clicks.SaveChangesAsync(cancellationToken);
        }

        return NoContent();
    }

    [HttpGet("metrics")]
    [HttpGet("tracing")]
    public IActionResult GetMetrics()
    {
        var metrics = _tracingService.GetMetrics(HttpContext);
        return Ok(metrics);
    }
}
