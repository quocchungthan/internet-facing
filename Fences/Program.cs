using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Fences.Data;
using Fences.Models;
using Fences.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.HttpOverrides;
using OpenIddict.Abstractions;

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<IdentityAppOptions>(builder.Configuration.GetSection("IdentityApp"));
var identityOptions = builder.Configuration.GetSection("IdentityApp").Get<IdentityAppOptions>() ?? new IdentityAppOptions();
var persistentLoginDays = identityOptions.PersistentLoginDays > 0 ? identityOptions.PersistentLoginDays : 30;
var dataProtectionKeysPath = Path.GetFullPath(identityOptions.DataProtectionKeysPath);
var identityDatabasePath = Path.GetFullPath(identityOptions.IdentityDatabasePath);
Directory.CreateDirectory(Path.GetDirectoryName(dataProtectionKeysPath)!);
Directory.CreateDirectory(Path.GetDirectoryName(identityDatabasePath)!);

builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));
builder.Services.AddDbContext<IdentityDbContext>(options =>
{
    options.UseSqlite($"Data Source={identityDatabasePath}");
    options.UseOpenIddict();
});

builder.Services.AddOpenIddict()
    .AddCore(options => options.UseEntityFrameworkCore().UseDbContext<IdentityDbContext>())
    .AddServer(options =>
    {
        options.SetIssuer(new Uri(identityOptions.OidcIssuer));
        options.SetAuthorizationEndpointUris("/connect/authorize");
        options.SetTokenEndpointUris("/connect/token");
        options.SetUserInfoEndpointUris("/connect/userinfo");
        options.AllowAuthorizationCodeFlow().RequireProofKeyForCodeExchange();
        options.RegisterScopes(OpenIddictConstants.Scopes.OpenId, OpenIddictConstants.Scopes.Profile, OpenIddictConstants.Scopes.Email);

        if (builder.Environment.IsDevelopment())
        {
            options.AddDevelopmentEncryptionCertificate()
                   .AddDevelopmentSigningCertificate();
        }
        else
        {
            options.AddEncryptionCertificate(LoadCertificate(identityOptions.OidcEncryptionCertificatePath, identityOptions.OidcEncryptionCertificatePassword, "encryption"));
            options.AddSigningCertificate(LoadCertificate(identityOptions.OidcSigningCertificatePath, identityOptions.OidcSigningCertificatePassword, "signing"));
        }

        options.UseAspNetCore(aspNetCore =>
        {
            aspNetCore.EnableAuthorizationEndpointPassthrough();
            aspNetCore.EnableUserInfoEndpointPassthrough();
            if (builder.Environment.IsDevelopment())
            {
                aspNetCore.DisableTransportSecurityRequirement();
            }
        });
    })
    .AddValidation(options =>
    {
        // Validates access tokens presented to /connect/userinfo issued by this same server.
        options.UseLocalServer();
        options.UseAspNetCore();
    });

builder.Services.AddSingleton<IAppCatalog, AppCatalog>();
builder.Services.AddSingleton<ReturnUrlPolicy>();
builder.Services.AddScoped<IGitHubRepositoryService, GitHubRepositoryService>();
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = "IdentityCookies";
    options.DefaultSignInScheme = "IdentityCookies";
    options.DefaultChallengeScheme = "GitHub";
})
.AddPolicyScheme("IdentityCookies", null, options =>
{
    options.ForwardDefaultSelector = context =>
        context.Request.Host.Host.Equals("identity.eldervibe.dev", StringComparison.OrdinalIgnoreCase)
            ? "CanonicalCookie"
            : "LegacyCookie";
})
.AddCookie("LegacyCookie", options =>
{
    options.Cookie.Name = "shuneo.identity";
    var configuredDomain = builder.Configuration["IdentityApp:CookieDomain"];
    if (!string.IsNullOrWhiteSpace(configuredDomain) && !configuredDomain.Equals("localhost", StringComparison.OrdinalIgnoreCase))
    {
        options.Cookie.Domain = configuredDomain;
    }

    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromDays(persistentLoginDays);
    options.Cookie.MaxAge = options.ExpireTimeSpan;
    options.SlidingExpiration = true;
    options.LoginPath = "/auth/login";
    options.Events.OnRedirectToLogin = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
})
.AddCookie("CanonicalCookie", options =>
{
    options.Cookie.Name = "eldervibe.identity";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromDays(persistentLoginDays);
    options.Cookie.MaxAge = options.ExpireTimeSpan;
    options.SlidingExpiration = true;
    options.LoginPath = "/auth/login";
    options.Events.OnRedirectToLogin = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
})
.AddOAuth("GitHub", options =>
{
    options.SignInScheme = "IdentityCookies";
    options.ClientId = builder.Configuration["Authentication:GitHub:ClientId"] ?? string.Empty;
    options.ClientSecret = builder.Configuration["Authentication:GitHub:ClientSecret"] ?? string.Empty;
    options.CallbackPath = "/signin-github";
    options.AuthorizationEndpoint = "https://github.com/login/oauth/authorize";
    options.TokenEndpoint = "https://github.com/login/oauth/access_token";
    options.UserInformationEndpoint = "https://api.github.com/user";
    options.Scope.Add("read:user");
    options.Scope.Add("user:email");
    options.Scope.Add("repo");
    options.SaveTokens = true;
    options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
    options.ClaimActions.MapJsonKey(ClaimTypes.Name, "name");
    options.ClaimActions.MapJsonKey("urn:github:login", "login");
    options.ClaimActions.MapJsonKey("urn:github:avatar", "avatar_url");
    options.ClaimActions.MapJsonKey(ClaimTypes.Email, "email");
    options.Events = new OAuthEvents
    {
        OnCreatingTicket = async context =>
        {
            var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.UserAgent.ParseAdd("fences-identity-app");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);
            var response = await context.Backchannel.SendAsync(request, context.HttpContext.RequestAborted);
            response.EnsureSuccessStatusCode();
            using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(context.HttpContext.RequestAborted));
            context.RunClaimActions(payload.RootElement);
            var verifiedEmail = await GetVerifiedGitHubEmailAsync(context, context.Identity?.FindFirst(ClaimTypes.Email)?.Value);
            if (!string.IsNullOrWhiteSpace(verifiedEmail) && context.Identity is not null)
            {
                ReplaceClaim(context.Identity, ClaimTypes.Email, verifiedEmail);
                ReplaceClaim(context.Identity, "urn:github:email_verified", "true");
            }

            if (string.IsNullOrWhiteSpace(context.Identity?.FindFirst(ClaimTypes.Name)?.Value))
            {
                var login = context.Identity?.FindFirst("urn:github:login")?.Value;
                if (!string.IsNullOrWhiteSpace(login))
                {
                    context.Identity?.AddClaim(new Claim(ClaimTypes.Name, login));
                }
            }
        }
    };
});

builder.Services.AddAuthorization();
builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});
builder.Services.AddCors(options => options.AddPolicy("IdentityCors", policy =>
{
    var allowedOrigins = builder.Configuration.GetSection("IdentityApp:AllowedCorsOrigins").Get<string[]>() ?? [];
    policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
}));

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.EnsureCreated();
    await SeedOidcClientsAsync(scope.ServiceProvider, identityOptions.OidcClients);
}
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/");
    app.UseHsts();
}

app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseCors("IdentityCors");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

static X509Certificate2 LoadCertificate(string path, string password, string purpose)
{
    if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
    {
        throw new InvalidOperationException($"A production OIDC {purpose} certificate path is required and must exist.");
    }

    return X509CertificateLoader.LoadPkcs12FromFile(path, password, X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.EphemeralKeySet);
}

static async Task SeedOidcClientsAsync(IServiceProvider services, IEnumerable<OidcClientOptions> clients)
{
    var manager = services.GetRequiredService<IOpenIddictApplicationManager>();
    foreach (var client in clients.Where(client => !string.IsNullOrWhiteSpace(client.ClientId)))
    {
        // A non-empty secret registers a confidential client; otherwise the client stays public.
        var isConfidential = !string.IsNullOrWhiteSpace(client.ClientSecret);

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = client.ClientId,
            ClientSecret = isConfidential ? client.ClientSecret : null,
            DisplayName = string.IsNullOrWhiteSpace(client.DisplayName) ? client.ClientId : client.DisplayName,
            ClientType = isConfidential ? OpenIddictConstants.ClientTypes.Confidential : OpenIddictConstants.ClientTypes.Public,
            ConsentType = OpenIddictConstants.ConsentTypes.Implicit
        };
        descriptor.RedirectUris.UnionWith(client.RedirectUris.Select(uri => new Uri(uri)));
        descriptor.PostLogoutRedirectUris.UnionWith(client.PostLogoutRedirectUris.Select(uri => new Uri(uri)));
        descriptor.Permissions.UnionWith([
            OpenIddictConstants.Permissions.Endpoints.Authorization,
            OpenIddictConstants.Permissions.Endpoints.Token,
            OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
            OpenIddictConstants.Permissions.ResponseTypes.Code,
            OpenIddictConstants.Permissions.Scopes.Profile,
            OpenIddictConstants.Permissions.Scopes.OpenId,
            OpenIddictConstants.Permissions.Scopes.Email
        ]);

        var existingApplication = await manager.FindByClientIdAsync(client.ClientId);
        if (existingApplication is null)
        {
            await manager.CreateAsync(descriptor);
        }
        else
        {
            // client_type is not mutable in-place for existing rows in every OpenIddict version;
            // UpdateAsync re-applies the descriptor (including the new secret hash) to the existing entity.
            await manager.UpdateAsync(existingApplication, descriptor);
        }
    }
}

static async Task<string?> GetVerifiedGitHubEmailAsync(OAuthCreatingTicketContext context, string? currentEmail)
{
    if (string.IsNullOrWhiteSpace(context.AccessToken))
    {
        return null;
    }

    var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user/emails");
    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    request.Headers.UserAgent.ParseAdd("fences-identity-app");
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);
    var response = await context.Backchannel.SendAsync(request, context.HttpContext.RequestAborted);
    if (!response.IsSuccessStatusCode)
    {
        return null;
    }

    using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(context.HttpContext.RequestAborted));
    var verifiedEmails = payload.RootElement.EnumerateArray()
        .Select(email => new
        {
            Address = email.TryGetProperty("email", out var address) ? address.GetString() : null,
            Primary = email.TryGetProperty("primary", out var primary) && primary.GetBoolean(),
            Verified = email.TryGetProperty("verified", out var verified) && verified.GetBoolean()
        })
        .Where(email => email.Verified && !string.IsNullOrWhiteSpace(email.Address))
        .ToList();

    return verifiedEmails.FirstOrDefault(email => email.Address!.Equals(currentEmail, StringComparison.OrdinalIgnoreCase))?.Address
        ?? verifiedEmails.FirstOrDefault(email => email.Primary)?.Address
        ?? verifiedEmails.FirstOrDefault()?.Address;
}

static void ReplaceClaim(ClaimsIdentity identity, string type, string value)
{
    foreach (var claim in identity.FindAll(type).ToList())
    {
        identity.RemoveClaim(claim);
    }

    identity.AddClaim(new Claim(type, value));
}

