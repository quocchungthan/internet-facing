using System.Security.Claims;
using Farm.Auth;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddFarmApiAuth(builder.Configuration);

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/healthz", () => Results.Ok(new { status = "ok", service = "farm-mind" }));

// MCP clients read this on a 401 to find Fences and open the browser sign-in.
app.MapProtectedResourceMetadata("/mcp");

// Placeholder for the MCP server (Streamable HTTP). Bearer-only: a missing or invalid token yields
// 401 + WWW-Authenticate with the resource metadata URL, which is what starts the web login.
app.MapMethods("/mcp", ["GET", "POST", "DELETE"], (ClaimsPrincipal user) => Results.Problem(
        title: "MCP tools are not implemented yet.",
        detail: $"Authenticated as {user.FindFirstValue("sub")}.",
        statusCode: StatusCodes.Status501NotImplemented))
    .RequireAuthorization(FarmAuthSchemes.McpPolicy);

// REST surface for the Farm UI and other clients (session cookie or bearer token).
var memory = app.MapGroup("/api/memory").RequireAuthorization(FarmAuthSchemes.UserPolicy);
memory.MapGet("/whoami", (ClaimsPrincipal user) => Results.Ok(new
{
    subject = user.FindFirstValue("sub"),
    name = user.FindFirstValue("name")
}));

app.Run();

public partial class Program;
