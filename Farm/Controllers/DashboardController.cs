using Microsoft.AspNetCore.Mvc;
using Farm.Auth;

namespace Farm.Controllers;

[ApiController]
[Route("api/dashboard")]
public sealed class DashboardController(IConfiguration configuration, FarmAuthOptions auth) : ControllerBase
{
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult Get(int days = 30)
    {
        if (User.Identity?.IsAuthenticated != true) return Unauthorized();
        if (days is not (7 or 30 or 90))
            return BadRequest(new { error = "Range must be 7, 30 or 90 days." });

        // Fences subjects are github:<immutable GitHub ID>, not a mutable username or email.
        var subject = User.FindFirst("sub")?.Value;
        var owners = configuration.GetSection("Dashboard:OwnerSubjects").Get<string[]>() ?? [];
        var isOwner = subject is not null && owners.Contains(subject, StringComparer.Ordinal);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        string[] names = ["Review a change", "Summarize project context", "Generate focused tests", "Refactor a component"];
        var tasks = Enumerable.Range(0, days)
            .Where(offset => offset % 7 != 6)
            .Select(offset =>
            {
                var cost = 0.12m + offset % 5 * 0.08m;
                return new
                {
                    id = $"demo-task-{offset}",
                    date = today.AddDays(-offset),
                    name = names[offset % names.Length],
                    model = offset % 3 == 0 ? "Reasoning model (demo)" : "Fast model (demo)",
                    inputTokens = 8000 + offset % 6 * 1200,
                    outputTokens = 1200 + offset % 5 * 350,
                    durationSeconds = 40 + offset % 8 * 25,
                    inputBytes = 32000 + offset % 6 * 4800,
                    outputBytes = 4800 + offset % 5 * 1400,
                    costUsd = cost,
                    feeUsd = 0.01m,
                    baselineCostUsd = cost * 1.8m
                };
            }).ToArray();

        object? audience = null;
        if (isOwner)
        {
            audience = new
            {
                source = "fences",
                url = auth.Enabled
                    ? $"{auth.NormalizedAuthority}/api/audience?clientId={Uri.EscapeDataString(auth.ClientId!)}&days={days}"
                    : null
            };
        }

        return Ok(new
        {
            source = "demo",
            currency = "USD",
            days,
            from = today.AddDays(1 - days),
            through = today,
            isOwner,
            tasks,
            audience
        });
    }
}
