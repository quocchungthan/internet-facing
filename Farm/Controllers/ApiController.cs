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

    [HttpGet("metrics")]
    [HttpGet("tracing")]
    public IActionResult GetMetrics()
    {
        var metrics = _tracingService.GetMetrics(HttpContext);
        return Ok(metrics);
    }
}
