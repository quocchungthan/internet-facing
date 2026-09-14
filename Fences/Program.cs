using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Fences.Models;
using Fences.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<IdentityAppOptions>(builder.Configuration.GetSection("IdentityApp"));
var identityOptions = builder.Configuration.GetSection("IdentityApp").Get<IdentityAppOptions>() ?? new IdentityAppOptions();
var persistentLoginDays = identityOptions.PersistentLoginDays > 0 ? identityOptions.PersistentLoginDays : 30;

builder.Services.AddSingleton<IAppCatalog, AppCatalog>();
builder.Services.AddSingleton<ReturnUrlPolicy>();
builder.Services.AddScoped<IGitHubRepositoryService, GitHubRepositoryService>();
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = "GitHub";
})
.AddCookie(options =>
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
.AddOAuth("GitHub", options =>
{
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
