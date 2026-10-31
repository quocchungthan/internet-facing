using System.Security.Claims;
using Farm.Auth;
using Farm.Mind.Data;
using Farm.Mind.Mcp;
using Farm.Mind.Notes;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddFarmApiAuth(builder.Configuration);

// Shares Farm's database (own "mind" schema and migration history). Same POSTGRES_* settings as Farm.
builder.Services.AddDbContext<MindDbContext>(options =>
{
    var host = Environment.GetEnvironmentVariable("POSTGRES_HOST") ?? "localhost";
    var port = Environment.GetEnvironmentVariable("POSTGRES_PORT") ?? "4554";
    var database = Environment.GetEnvironmentVariable("POSTGRES_DB") ?? "aurafarming";
    var username = Environment.GetEnvironmentVariable("POSTGRES_USER") ?? "postgres";
    var connection = new Npgsql.NpgsqlConnectionStringBuilder
    {
        Host = host,
        Port = int.Parse(port),
        Database = database,
        Username = username,
        Password = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD") ?? "postgres"
    };
    options.UseNpgsql(connection.ConnectionString, npgsql => npgsql.MigrationsHistoryTable("__MindMigrationsHistory", MindDbContext.Schema));
});
builder.Services.AddScoped<MemoryStore>();
builder.Services.AddScoped<McpUsageStore>();
builder.Services.AddScoped<AccessKeyStore>();
builder.Services.AddScoped<Farm.Auth.IAccessKeyValidator>(sp => sp.GetRequiredService<AccessKeyStore>());
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<NoteDatabase>();
builder.Services.AddScoped<NoteDecoder>();
builder.Services.AddScoped<NotePortal>();
builder.Services.AddMcpServer(options => options.ServerInstructions = MindGuidance.Text)
    .WithHttpTransport(http => http.Stateless = true)
    .WithTools<MindTools>()
    .WithTools<NoteTools>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MindDbContext>();
    if (db.Database.IsRelational())
        await db.Database.MigrateAsync();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<McpUsageMiddleware>();

app.MapGet("/healthz", () => Results.Ok(new { status = "ok", service = "farm-mind" }));

// MCP clients read this on a 401 to find Fences and open the browser sign-in.
app.MapProtectedResourceMetadata("/mcp");

// MCP server (Streamable HTTP). Bearer-only: a missing or invalid token yields 401 + WWW-Authenticate
// with the resource metadata URL, which is what starts the web login.
app.MapMcp("/mcp").RequireAuthorization(FarmAuthSchemes.McpPolicy);

// REST surface for the Farm UI and other clients (session cookie or bearer token).
var memory = app.MapGroup("/api/memory").RequireAuthorization(FarmAuthSchemes.UserPolicy);
memory.MapGet("/whoami", (ClaimsPrincipal user) => Results.Ok(new
{
    subject = user.FindFirstValue("sub"),
    name = user.FindFirstValue("name")
}));

static string Subject(ClaimsPrincipal user) => user.FindFirstValue("sub")!;

memory.MapGet("", async (MemoryStore store, ClaimsPrincipal user, string? q, string? workspace, int? limit, CancellationToken ct) =>
    Results.Ok(await store.SearchAsync(Subject(user), q ?? "", workspace, limit ?? 10, ct)));
memory.MapGet("/briefing", async (MemoryStore store, ClaimsPrincipal user, CancellationToken ct) =>
    Results.Ok(await store.BriefingAsync(Subject(user), ct)));
memory.MapGet("/usage", async (McpUsageStore store, ClaimsPrincipal user, int? days, CancellationToken ct) =>
{
    var range = days ?? 30;
    if (range is not (7 or 30 or 90)) return Results.BadRequest(new { error = "Range must be 7, 30 or 90 days." });
    return Results.Ok(await store.GetAsync(Subject(user), range, ct));
});
memory.MapGet("/{id:guid}", async (Guid id, MemoryStore store, ClaimsPrincipal user, CancellationToken ct) =>
    await store.GetAsync(Subject(user), id, ct) is { } found ? Results.Ok(found) : Results.NotFound());
memory.MapDelete("/{id:guid}", async (Guid id, MemoryStore store, ClaimsPrincipal user, CancellationToken ct) =>
    await store.ForgetAsync(Subject(user), id, ct) ? Results.NoContent() : Results.NotFound());

// Access keys for clients that cannot do the browser sign-in (e.g. Copilot CLI). Managing them needs the
// browser session, so a key can never mint more keys.
var keys = app.MapGroup("/api/memory/keys")
    .RequireAuthorization(policy => policy.AddAuthenticationSchemes(FarmAuthSchemes.Cookie).RequireAuthenticatedUser());
keys.MapGet("", async (AccessKeyStore store, ClaimsPrincipal user, CancellationToken ct) =>
    Results.Ok(await store.ListAsync(Subject(user), ct)));
keys.MapPost("", async (AccessKeyStore store, ClaimsPrincipal user, CreateKeyRequest request, CancellationToken ct) =>
{
    try { return Results.Ok(await store.CreateAsync(Subject(user), request.Name ?? "", ct)); }
    catch (ArgumentException e) { return Results.BadRequest(new { error = e.Message }); }
});
keys.MapDelete("/{id:guid}", async (Guid id, AccessKeyStore store, ClaimsPrincipal user, CancellationToken ct) =>
    await store.RevokeAsync(Subject(user), id, ct) ? Results.NoContent() : Results.NotFound());

app.Run();

public record CreateKeyRequest(string? Name);

public partial class Program;
