using System.Security.Claims;
using Fences.Services;
using Microsoft.AspNetCore.Mvc;

namespace Fences.Controllers;

[ApiController]
[Route("api/audience")]
public sealed class AudienceController(AudienceStore store, IConfiguration configuration) : ControllerBase
{
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Get(string clientId, int days = 30, CancellationToken ct = default)
    {
        if (User.Identity?.IsAuthenticated != true) return Unauthorized();
        var subject = "github:" + User.FindFirstValue(ClaimTypes.NameIdentifier);
        var owners = configuration.GetSection("IdentityApp:AudienceOwnerSubjects").Get<string[]>()
            ?? ["github:44728650"];
        if (!owners.Contains(subject, StringComparer.Ordinal)) return StatusCode(403);
        if (days is not (7 or 30 or 90) || string.IsNullOrWhiteSpace(clientId))
            return BadRequest(new { error = "Specify clientId and a range of 7, 30 or 90 days." });
        return Ok(await store.ReadAsync(clientId, days, ct));
    }
}
