using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Farm.Auth;

public static class FarmAuthExtensions
{
    public const string ProtectedResourceMetadataPath = "/.well-known/oauth-protected-resource";

    public static FarmAuthOptions GetFarmAuthOptions(this IConfiguration configuration)
    {
        var options = new FarmAuthOptions();
        configuration.GetSection(FarmAuthOptions.SectionName).Bind(options);
        return options;
    }

    /// <summary>
    /// Browser sign-in for the Farm UI: a session cookie, and an OIDC code+PKCE challenge against Fences
    /// when <c>FarmAuth:Authority</c> and <c>FarmAuth:ClientId</c> are set. Public pages stay anonymous.
    /// </summary>
    public static IServiceCollection AddFarmBrowserAuth(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetFarmAuthOptions();
        services.AddSingleton(options);
        AddSharedDataProtection(services, options);

        var auth = services.AddAuthentication(FarmAuthSchemes.Cookie);
        auth.AddCookie(FarmAuthSchemes.Cookie, cookie => ConfigureCookie(cookie, options, apiMode: false));

        if (options.Enabled)
        {
            auth.AddOpenIdConnect(FarmAuthSchemes.Oidc, oidc => ConfigureOidc(oidc, options));
        }

        services.AddAuthorization();
        return services;
    }

    /// <summary>
    /// API/MCP auth: the shared session cookie (Farm UI calls) or a Fences-issued bearer token
    /// (MCP and other clients). Unauthenticated calls get 401, never a redirect.
    /// </summary>
    public static IServiceCollection AddFarmApiAuth(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetFarmAuthOptions();
        services.AddSingleton(options);
        AddSharedDataProtection(services, options);

        services.AddAuthentication(FarmAuthSchemes.Auto)
            .AddPolicyScheme(FarmAuthSchemes.Auto, displayName: null, policy =>
            {
                policy.ForwardDefaultSelector = context =>
                    context.Request.Headers.Authorization.Count > 0 ? FarmAuthSchemes.Bearer : FarmAuthSchemes.Cookie;
            })
            .AddCookie(FarmAuthSchemes.Cookie, cookie => ConfigureCookie(cookie, options, apiMode: true))
            .AddJwtBearer(FarmAuthSchemes.Bearer, bearer => ConfigureBearer(bearer, options));

        services.AddAuthorizationBuilder()
            .AddPolicy(FarmAuthSchemes.UserPolicy, policy => policy
                .AddAuthenticationSchemes(FarmAuthSchemes.Auto)
                .RequireAuthenticatedUser())
            .AddPolicy(FarmAuthSchemes.McpPolicy, policy => policy
                .AddAuthenticationSchemes(FarmAuthSchemes.Bearer)
                .RequireAuthenticatedUser());
        return services;
    }

    private static void AddSharedDataProtection(IServiceCollection services, FarmAuthOptions options)
    {
        var dataProtection = services.AddDataProtection().SetApplicationName(options.ApplicationName);
        if (!string.IsNullOrWhiteSpace(options.DataProtectionKeysPath))
        {
            dataProtection.PersistKeysToFileSystem(new DirectoryInfo(options.DataProtectionKeysPath));
        }
    }

    private static void ConfigureCookie(CookieAuthenticationOptions cookie, FarmAuthOptions options, bool apiMode)
    {
        cookie.Cookie.Name = options.CookieName;
        cookie.Cookie.HttpOnly = true;
        cookie.Cookie.SameSite = SameSiteMode.Lax;
        cookie.Cookie.SecurePolicy = options.PublicBaseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? CookieSecurePolicy.Always
            : CookieSecurePolicy.SameAsRequest;
        if (!string.IsNullOrWhiteSpace(options.CookieDomain))
        {
            cookie.Cookie.Domain = options.CookieDomain;
        }

        cookie.ExpireTimeSpan = TimeSpan.FromDays(14);
        cookie.SlidingExpiration = true;

        if (apiMode)
        {
            cookie.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            cookie.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        }
    }

    private static void ConfigureOidc(OpenIdConnectOptions oidc, FarmAuthOptions options)
    {
        oidc.SignInScheme = FarmAuthSchemes.Cookie;
        oidc.Authority = options.NormalizedAuthority;
        oidc.ClientId = options.ClientId;
        oidc.ClientSecret = options.ClientSecret;
        oidc.RequireHttpsMetadata = options.NormalizedAuthority.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        oidc.ResponseType = "code";
        oidc.UsePkce = true;
        oidc.SaveTokens = false;
        oidc.GetClaimsFromUserInfoEndpoint = false;
        oidc.MapInboundClaims = false;
        oidc.CallbackPath = "/signin-oidc";
        oidc.Scope.Clear();
        oidc.Scope.Add("openid");
        oidc.Scope.Add("profile");
        oidc.Scope.Add("email");
        oidc.TokenValidationParameters.NameClaimType = "name";

        // The registered redirect URI must match exactly, so pin it to the public origin
        // instead of trusting whatever host the reverse proxy forwards.
        var callback = options.PublicBaseUrl.TrimEnd('/') + oidc.CallbackPath;
        oidc.Events.OnRedirectToIdentityProvider = context =>
        {
            context.ProtocolMessage.RedirectUri = callback;
            return Task.CompletedTask;
        };
        oidc.Events.OnRedirectToIdentityProviderForSignOut = context =>
        {
            context.HandleResponse();
            return Task.CompletedTask;
        };
    }

    private static void ConfigureBearer(JwtBearerOptions bearer, FarmAuthOptions options)
    {
        bearer.Authority = options.NormalizedAuthority;
        bearer.Audience = options.ResolvedAudience;
        bearer.MapInboundClaims = false;
        bearer.RequireHttpsMetadata = options.NormalizedAuthority.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        bearer.TokenValidationParameters.NameClaimType = "name";

        // RFC 9728 / MCP: point clients at the protected resource metadata so they can find
        // Fences and start the browser login on their own.
        var metadataUrl = options.PublicBaseUrl.TrimEnd('/') + ProtectedResourceMetadataPath;
        bearer.Events = new JwtBearerEvents
        {
            OnChallenge = context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                var error = string.IsNullOrEmpty(context.Error) ? string.Empty : $", error=\"{context.Error}\"";
                context.Response.Headers.WWWAuthenticate = $"Bearer resource_metadata=\"{metadataUrl}\"{error}";
                return Task.CompletedTask;
            }
        };
    }

    /// <summary>
    /// <c>GET /auth/login</c>, <c>POST /auth/logout</c> and <c>GET /api/me</c> for the Farm UI.
    /// </summary>
    public static IEndpointRouteBuilder MapFarmAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/auth/login", (HttpContext context, FarmAuthOptions options, string? returnUrl) =>
        {
            if (!options.Enabled)
            {
                return Results.Problem("Sign-in is not configured.", statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            var properties = new AuthenticationProperties { RedirectUri = ReturnUrl.Sanitize(returnUrl) };
            return Results.Challenge(properties, [FarmAuthSchemes.Oidc]);
        });

        endpoints.MapPost("/auth/logout", async (HttpContext context, FarmAuthOptions options, string? returnUrl) =>
        {
            await context.SignOutAsync(FarmAuthSchemes.Cookie);

            var back = options.PublicBaseUrl.TrimEnd('/') + ReturnUrl.Sanitize(returnUrl);
            if (!options.Enabled)
            {
                return Results.Redirect(back);
            }

            // Also clear the Fences single sign-on cookie so the next sign-in asks again.
            var fencesLogout = $"{options.NormalizedAuthority}/auth/logout?returnUrl={Uri.EscapeDataString(back)}";
            return Results.Redirect(fencesLogout);
        });

        endpoints.MapGet("/api/me", (HttpContext context, FarmAuthOptions options) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            var user = context.User;
            if (user.Identity?.IsAuthenticated != true)
            {
                return Results.Ok(new MeResponse(false, options.Enabled, null, null, null, null, null));
            }

            return Results.Ok(new MeResponse(
                true,
                options.Enabled,
                user.FindFirst("sub")?.Value,
                user.FindFirst("name")?.Value,
                user.FindFirst("preferred_username")?.Value,
                user.FindFirst("picture")?.Value,
                user.FindFirst("email")?.Value));
        });

        return endpoints;
    }

    /// <summary>
    /// RFC 9728 protected resource metadata. This is the hook MCP clients use to discover Fences
    /// and begin the browser sign-in.
    /// </summary>
    public static IEndpointRouteBuilder MapProtectedResourceMetadata(this IEndpointRouteBuilder endpoints, string resourcePath = "/mcp")
    {
        IResult Metadata(FarmAuthOptions options) => Results.Json(new
        {
            resource = options.PublicBaseUrl.TrimEnd('/') + resourcePath,
            authorization_servers = options.Enabled ? new[] { options.NormalizedAuthority } : [],
            bearer_methods_supported = new[] { "header" },
            scopes_supported = new[] { "openid", "profile", "email" },
            resource_name = "Elder Vibe AI memory"
        });

        endpoints.MapGet(ProtectedResourceMetadataPath, Metadata);
        endpoints.MapGet(ProtectedResourceMetadataPath + resourcePath, Metadata);
        return endpoints;
    }
}

public sealed record MeResponse(
    bool IsAuthenticated,
    bool SignInAvailable,
    string? Subject,
    string? Name,
    string? Username,
    string? Picture,
    string? Email);
