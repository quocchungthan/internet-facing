using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.FileProviders;
using OpensourceLab.FileStorage.ServedServices;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Shed.Data;
using Shed.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

var vanillaConnectionString = builder.Configuration.GetConnectionString("VanillaConnection") ?? throw new InvalidOperationException("Connection string 'VanillaConnection' not found.");
builder.Services.AddDbContext<VanillaDbContext>(options =>
    options.UseSqlite(vanillaConnectionString));

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo("/keys"));

builder.Services.AddScoped<IServeFiles, ServeFilesService>();
builder.Services.AddScoped<IServeAcl, ServeAclService>();
builder.Services.AddScoped<IServeUserPreferences, ServeUserPreferencesService>();
builder.Services.AddSingleton<ILoginBackgroundResolver, LoginBackgroundResolver>();
builder.Services.AddSingleton<ISyncStateStore, FileSyncStateStore>();
builder.Services.AddScoped<IAccessKeyValidator, AccessKeyValidator>();
builder.Services.AddScoped<IFileSyncService, FileSyncService>();
builder.Services.AddScoped<ShareLinkService>();
builder.Services.AddScoped<IStorageOwnerResolver, StorageOwnerResolver>();
builder.Services.Configure<IdentityBridgeOptions>(builder.Configuration.GetSection(IdentityBridgeOptions.Section));
builder.Services.AddHttpClient("IdentityBridge");
builder.Services.AddScoped<IIdentitySessionClient, IdentitySessionClient>();

// Email
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection(SmtpOptions.Section));
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<IdentityUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = true;
    options.SignIn.RequireConfirmedEmail = true;
})
    .AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/auth/login";
});

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 10L * 1024 * 1024 * 1024;
});


builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());
builder.Services.AddControllers();
builder.Services.AddRazorPages();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    services.GetRequiredService<ApplicationDbContext>().Database.Migrate();
    services.GetRequiredService<VanillaDbContext>().Database.Migrate();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

var prodStoragePath = builder.Configuration["Storage:RootPath"]
    ?? @"/app/data/prodstorage";

if (Directory.Exists(prodStoragePath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(prodStoragePath),
        RequestPath = "/ProdStorage"
    });
}

app.UseRouting();

app.UseAuthentication();
app.Use(async (context, next) =>
{
    var identityBridgeOptions = context.RequestServices.GetRequiredService<Microsoft.Extensions.Options.IOptions<IdentityBridgeOptions>>().Value;

    if (identityBridgeOptions.Enabled && context.Request.Path.Equals("/Identity/Account/Logout", StringComparison.OrdinalIgnoreCase))
    {
        var requestedReturnUrl = context.Request.Query["returnUrl"].ToString();
        if (string.IsNullOrWhiteSpace(requestedReturnUrl) && context.Request.HasFormContentType)
        {
            var form = await context.Request.ReadFormAsync(context.RequestAborted);
            requestedReturnUrl = form["returnUrl"].ToString();
        }

        var localReturnPath = NormalizeLocalReturnPath(requestedReturnUrl) ?? "/";
        context.Response.Redirect($"/auth/logout?returnUrl={UrlEncoder.Default.Encode(localReturnPath)}");
        return;
    }

    if (!identityBridgeOptions.Enabled)
    {
        await next();
        return;
    }

    var endpoint = context.GetEndpoint();
    var allowsAnonymous = endpoint?.Metadata?.GetMetadata<IAllowAnonymous>() is not null;
    if (allowsAnonymous)
    {
        await next();
        return;
    }

    var identitySessionClient = context.RequestServices.GetRequiredService<IIdentitySessionClient>();
    var session = await identitySessionClient.GetSessionAsync(context.Request.Headers.Cookie.ToString(), context.RequestAborted);
    if (!session.IsAuthenticated)
    {
        var returnUrl = BuildCurrentUrl(context.Request);
        var identityBase = (identityBridgeOptions.BaseUrl ?? string.Empty).Trim().TrimEnd('/');
        var loginUrl = $"{identityBase}/?returnUrl={UrlEncoder.Default.Encode(returnUrl)}";

        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                isAuthenticated = false,
                loginUrl,
            }, cancellationToken: context.RequestAborted);
            return;
        }

        context.Response.Redirect(loginUrl);
        return;
    }

    if (context.User?.Identity?.IsAuthenticated != true)
    {
        var identityKey = session.GithubLogin?.Trim()
            ?? session.Email?.Trim()?.ToLowerInvariant()
            ?? session.Name?.Trim();

        var claims = new List<Claim>();
        if (!string.IsNullOrWhiteSpace(identityKey))
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, identityKey));
            claims.Add(new Claim("urn:identity:key", identityKey));
        }

        if (!string.IsNullOrWhiteSpace(session.Name))
        {
            claims.Add(new Claim(ClaimTypes.Name, session.Name));
        }

        if (!string.IsNullOrWhiteSpace(session.Email))
        {
            claims.Add(new Claim(ClaimTypes.Email, session.Email));
        }

        if (!string.IsNullOrWhiteSpace(session.GithubLogin))
        {
            claims.Add(new Claim("urn:github:login", session.GithubLogin));
        }

        var identity = new ClaimsIdentity(claims, "IdentityBridge");
        context.User = new ClaimsPrincipal(identity);
    }

    await next();
});
app.UseAuthorization();

app.MapGet("/public/login-background", (ILoginBackgroundResolver backgroundResolver) =>
{
    var physicalPath = backgroundResolver.ResolveBackgroundPhysicalPath();
    if (string.IsNullOrWhiteSpace(physicalPath) || !File.Exists(physicalPath))
    {
        return Results.NotFound();
    }

    var extension = Path.GetExtension(physicalPath).ToLowerInvariant();
    var contentType = extension switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        _ => "application/octet-stream"
    };

    return Results.File(physicalPath, contentType);
}).AllowAnonymous();

app.MapGet("/auth/login", (
    HttpContext context,
    Microsoft.Extensions.Options.IOptions<IdentityBridgeOptions> identityBridgeOptionsAccessor) =>
{
    var identityBridgeOptions = identityBridgeOptionsAccessor.Value;
    var requestedReturnUrl = context.Request.Query["returnUrl"].ToString();
    var localReturnPath = NormalizeLocalReturnPath(requestedReturnUrl) ?? "/Files";
    var localSignInUrl = $"/signin?returnUrl={UrlEncoder.Default.Encode(localReturnPath)}";

    if (!identityBridgeOptions.Enabled)
    {
        return Results.Redirect(localSignInUrl);
    }

    var identityBase = (identityBridgeOptions.BaseUrl ?? string.Empty).Trim().TrimEnd('/');
    if (string.IsNullOrWhiteSpace(identityBase))
    {
        return Results.Redirect(localSignInUrl);
    }

    var callbackUrl = BuildAbsoluteUrlFromLocalPath(context.Request, localReturnPath);
    var loginUrl = $"{identityBase}/?returnUrl={UrlEncoder.Default.Encode(callbackUrl)}";
    return Results.Redirect(loginUrl);
}).AllowAnonymous();

app.MapGet("/auth/logout", async (
    HttpContext context,
    Microsoft.Extensions.Options.IOptions<IdentityBridgeOptions> identityBridgeOptionsAccessor) =>
{
    // Clear local auth cookies first so storage does not appear logged-in during redirect hops.
    await context.SignOutAsync(IdentityConstants.ApplicationScheme);
    await context.SignOutAsync(IdentityConstants.ExternalScheme);

    // Best-effort cleanup for identity bridge cookie variants.
    context.Response.Cookies.Delete("shuneo.identity");
    context.Response.Cookies.Delete("shuneo.identity", new CookieOptions
    {
        Domain = ".shuneo.com",
        Path = "/"
    });

    var identityBridgeOptions = identityBridgeOptionsAccessor.Value;
    var requestedReturnUrl = context.Request.Query["returnUrl"].ToString();
    var localReturnPath = NormalizeLocalReturnPath(requestedReturnUrl)
        ?? "/";

    if (identityBridgeOptions.Enabled)
    {
        var identityBase = (identityBridgeOptions.BaseUrl ?? string.Empty).Trim().TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(identityBase))
        {
            var callbackUrl = BuildAbsoluteUrlFromLocalPath(context.Request, localReturnPath);
            var logoutUrl = $"{identityBase}/auth/logout?returnUrl={UrlEncoder.Default.Encode(callbackUrl)}";
            return Results.Redirect(logoutUrl);
        }
    }

    return Results.Redirect(localReturnPath);
}).AllowAnonymous();

app.MapGet("/file-content/{id:guid}", async (
    Guid id,
    bool? download,
    ClaimsPrincipal user,
    IConfiguration config,
    IStorageOwnerResolver storageOwnerResolver,
    VanillaDbContext db,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    var ownerResolution = await storageOwnerResolver.ResolveAsync(user, cancellationToken);
    var userId = ownerResolution.UserId;

    var storageRoot = config["Storage:RootPath"]
        ?? @"/app/data/prodstorage";
    var userFolder = SanitizeFolderSegment(userId);
    var userRoot = Path.Combine(storageRoot, userFolder);

    var fileItem = await db.FileItems
        .AsNoTracking()
        .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    if (fileItem is null)
    {
        return Results.NotFound();
    }

    if (!fileItem.ActualPath.StartsWith(userRoot, StringComparison.OrdinalIgnoreCase))
    {
        return Results.Forbid();
    }

    if (!File.Exists(fileItem.ActualPath))
    {
        return Results.NotFound();
    }

    if (download == true)
    {
        var fileName = Path.GetFileName(fileItem.ActualPath);
        return Results.File(fileItem.ActualPath, fileItem.ContentType, fileDownloadName: fileName);
    }

    return Results.File(fileItem.ActualPath, fileItem.ContentType);
}).RequireAuthorization();

// Path-based download: works for any file under the user's root,
// including filesystem-only items not tracked in the database.
app.MapGet("/api/files/download", async (
    string path,
    ClaimsPrincipal user,
    IConfiguration config,
    IStorageOwnerResolver storageOwnerResolver,
    CancellationToken cancellationToken) =>
{
    var ownerResolution = await storageOwnerResolver.ResolveAsync(user, cancellationToken);
    var userId = ownerResolution.UserId;

    var storageRoot = config["Storage:RootPath"]
        ?? @"/app/data/prodstorage";
    var userRoot = Path.Combine(storageRoot, SanitizeFolderSegment(userId));

    // Resolve virtual path to physical path, preventing path traversal
    var relativePart = path.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
    var physicalPath = Path.GetFullPath(Path.Combine(userRoot, relativePart));

    if (!physicalPath.StartsWith(Path.GetFullPath(userRoot), StringComparison.OrdinalIgnoreCase))
    {
        return Results.Forbid();
    }

    if (!File.Exists(physicalPath))
    {
        return Results.NotFound();
    }

    var fileName = Path.GetFileName(physicalPath);
    var ext = Path.GetExtension(fileName).ToLowerInvariant();
    var contentType = ext switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".svg" => "image/svg+xml",
        ".pdf" => "application/pdf",
        ".mp3" => "audio/mpeg",
        ".mp4" => "video/mp4",
        ".zip" => "application/zip",
        ".json" => "application/json",
        ".xml" => "application/xml",
        ".html" or ".htm" => "text/html",
        ".css" => "text/css",
        ".js" => "text/javascript",
        ".txt" or ".md" or ".log" or ".csv" => "text/plain",
        _ => "application/octet-stream"
    };

    return Results.File(physicalPath, contentType, fileDownloadName: fileName);
}).RequireAuthorization();

app.MapPost(FileSyncEndpoints.Handshake, async (
    SyncHandshakeRequest request,
    HttpContext httpContext,
    IAccessKeyValidator keyValidator,
    IFileSyncService syncService,
    CancellationToken cancellationToken) =>
{
    var accessKey = httpContext.Request.Headers[FileSyncHeaders.AccessKey].ToString();
    var keyValidation = await keyValidator.ValidateAsync(accessKey, null, cancellationToken);
    if (!keyValidation.IsValid)
    {
        return Results.Unauthorized();
    }
    if (!keyValidation.CanRead)
    {
        return Results.Forbid();
    }

    var response = await syncService.HandshakeAsync(request, keyValidation.AllowedPathWildcards, cancellationToken);
    return Results.Ok(response);
}).AllowAnonymous();

app.MapPost(FileSyncEndpoints.PullChanges, async (
    PullChangesRequest request,
    HttpContext httpContext,
    IConfiguration configuration,
    IAccessKeyValidator keyValidator,
    IFileSyncService syncService,
    CancellationToken cancellationToken) =>
{
    var accessKey = httpContext.Request.Headers[FileSyncHeaders.AccessKey].ToString();
    var keyValidation = await keyValidator.ValidateAsync(accessKey, null, cancellationToken);
    if (!keyValidation.IsValid)
    {
        return Results.Unauthorized();
    }
    if (!keyValidation.CanRead)
    {
        return Results.Forbid();
    }

    var userRoot = ResolveUserRootPath(configuration, keyValidation.UserId);
    var response = await syncService.PullChangesAsync(request, userRoot, keyValidation.AllowedPathWildcards, cancellationToken);
    return Results.Ok(response);
}).AllowAnonymous();

app.MapPost(FileSyncEndpoints.MetadataUpsert, async (
    MetadataUpsertRequest request,
    HttpContext httpContext,
    IAccessKeyValidator keyValidator,
    IFileSyncService syncService,
    CancellationToken cancellationToken) =>
{
    var accessKey = httpContext.Request.Headers[FileSyncHeaders.AccessKey].ToString();
    var keyValidation = await keyValidator.ValidateAsync(accessKey, request.Path, cancellationToken);
    if (!keyValidation.IsValid)
    {
        return Results.Unauthorized();
    }
    if (!keyValidation.CanWrite)
    {
        return Results.Forbid();
    }

    var response = await syncService.MetadataUpsertAsync(request, cancellationToken);
    return Results.Ok(response);
}).AllowAnonymous();

app.MapPost(FileSyncEndpoints.UploadChunk, async (
    UploadChunkRequest request,
    HttpContext httpContext,
    IConfiguration configuration,
    IAccessKeyValidator keyValidator,
    IFileSyncService syncService,
    CancellationToken cancellationToken) =>
{
    var accessKey = httpContext.Request.Headers[FileSyncHeaders.AccessKey].ToString();
    var keyValidation = await keyValidator.ValidateAsync(accessKey, request.Path, cancellationToken);
    if (!keyValidation.IsValid)
    {
        return Results.Unauthorized();
    }
    if (!keyValidation.CanWrite)
    {
        return Results.Forbid();
    }

    var userRoot = ResolveUserRootPath(configuration, keyValidation.UserId);
    var response = await syncService.UploadChunkAsync(request, userRoot, cancellationToken);
    return Results.Ok(response);
}).AllowAnonymous();

app.MapPost(FileSyncEndpoints.UploadCommit, async (
    UploadCommitRequest request,
    HttpContext httpContext,
    IConfiguration configuration,
    IAccessKeyValidator keyValidator,
    IFileSyncService syncService,
    CancellationToken cancellationToken) =>
{
    var accessKey = httpContext.Request.Headers[FileSyncHeaders.AccessKey].ToString();
    var keyValidation = await keyValidator.ValidateAsync(accessKey, request.Path, cancellationToken);
    if (!keyValidation.IsValid)
    {
        return Results.Unauthorized();
    }
    if (!keyValidation.CanWrite)
    {
        return Results.Forbid();
    }

    var userRoot = ResolveUserRootPath(configuration, keyValidation.UserId);
    var response = await syncService.UploadCommitAsync(request, userRoot, cancellationToken);
    return Results.Ok(response);
}).AllowAnonymous();

app.MapPost(FileSyncEndpoints.Tombstone, async (
    TombstoneRequest request,
    HttpContext httpContext,
    IConfiguration configuration,
    IAccessKeyValidator keyValidator,
    IFileSyncService syncService,
    CancellationToken cancellationToken) =>
{
    var accessKey = httpContext.Request.Headers[FileSyncHeaders.AccessKey].ToString();
    var keyValidation = await keyValidator.ValidateAsync(accessKey, request.Path, cancellationToken);
    if (!keyValidation.IsValid)
    {
        return Results.Unauthorized();
    }
    if (!keyValidation.CanWrite)
    {
        return Results.Forbid();
    }

    var userRoot = ResolveUserRootPath(configuration, keyValidation.UserId);
    var response = await syncService.TombstoneAsync(request, userRoot, cancellationToken);
    return Results.Ok(response);
}).AllowAnonymous();

app.MapStaticAssets().AllowAnonymous();
app.MapControllers();
app.MapRazorPages()
   .WithStaticAssets();

static string SanitizeFolderSegment(string value)
{
    var cleaned = value;
    foreach (var invalid in Path.GetInvalidFileNameChars())
    {
        cleaned = cleaned.Replace(invalid, '_');
    }

    cleaned = cleaned.Replace("/", "_").Replace("\\", "_");
    return string.IsNullOrWhiteSpace(cleaned) ? "user" : cleaned;
}

static string ResolveUserRootPath(IConfiguration configuration, string userId)
{
    var storageRoot = configuration["Storage:RootPath"]
        ?? @"/app/data/prodstorage";
    return Path.Combine(storageRoot, SanitizeFolderSegment(userId));
}

// Health check endpoint for monitoring and deployment verification
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
    .AllowAnonymous()
    .WithName("Health");

app.Run();

static string BuildCurrentUrl(HttpRequest request)
{
    var forwardedProto = request.Headers["X-Forwarded-Proto"].ToString().Split(',').FirstOrDefault()?.Trim();
    var forwardedHost = request.Headers["X-Forwarded-Host"].ToString().Split(',').FirstOrDefault()?.Trim();
    var scheme = !string.IsNullOrWhiteSpace(forwardedProto) ? forwardedProto : request.Scheme;
    var host = !string.IsNullOrWhiteSpace(forwardedHost) ? forwardedHost : request.Host.Value;
    return $"{scheme}://{host}{request.PathBase}{request.Path}{request.QueryString}";
}

static string BuildAbsoluteUrlFromLocalPath(HttpRequest request, string localPath)
{
    var forwardedProto = request.Headers["X-Forwarded-Proto"].ToString().Split(',').FirstOrDefault()?.Trim();
    var forwardedHost = request.Headers["X-Forwarded-Host"].ToString().Split(',').FirstOrDefault()?.Trim();
    var scheme = !string.IsNullOrWhiteSpace(forwardedProto) ? forwardedProto : request.Scheme;
    var host = !string.IsNullOrWhiteSpace(forwardedHost) ? forwardedHost : request.Host.Value;

    var normalized = localPath.StartsWith('/') ? localPath : $"/{localPath}";
    return $"{scheme}://{host}{request.PathBase}{normalized}";
}

static string? NormalizeLocalReturnPath(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return null;
    }

    var trimmed = value.Trim();
    if (!trimmed.StartsWith('/'))
    {
        return null;
    }

    if (trimmed.StartsWith("//", StringComparison.Ordinal) || trimmed.StartsWith("/\\", StringComparison.Ordinal))
    {
        return null;
    }

    if (Uri.TryCreate(trimmed, UriKind.Absolute, out _))
    {
        return null;
    }

    return trimmed;
}


