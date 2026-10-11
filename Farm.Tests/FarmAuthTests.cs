using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Farm.Auth;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Xunit;

namespace Farm.Tests;

public class FarmAuthTests : IClassFixture<WebApplicationFactory<Program>>
{
    static FarmAuthTests()
    {
        Environment.SetEnvironmentVariable("USE_IN_MEMORY_DATABASE", "true");
    }

    private const string Authority = "https://identity.test";
    private readonly WebApplicationFactory<Program> _factory;

    public FarmAuthTests(WebApplicationFactory<Program> factory) => _factory = factory;

    private HttpClient CreateClient(bool configured)
    {
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("FarmAuth:Authority", configured ? Authority : "");
            builder.UseSetting("FarmAuth:ClientId", configured ? "farm" : "");
            builder.UseSetting("FarmAuth:PublicBaseUrl", "https://eldervibe.dev");
            builder.ConfigureServices(services =>
            {
                // Skip discovery: supply the endpoints directly.
                services.PostConfigure<OpenIdConnectOptions>(FarmAuthSchemes.Oidc, options =>
                {
                    var configuration = new OpenIdConnectConfiguration
                    {
                        Issuer = Authority,
                        AuthorizationEndpoint = Authority + "/connect/authorize",
                        TokenEndpoint = Authority + "/connect/token"
                    };
                    options.Configuration = configuration;
                    options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
                });
            });
        });

        return factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task Me_WhenAnonymous_ReportsSignedOut()
    {
        var response = await CreateClient(configured: true).GetAsync("/api/me");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(json.GetProperty("isAuthenticated").GetBoolean());
        Assert.True(json.GetProperty("signInAvailable").GetBoolean());
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task Me_WhenSignInNotConfigured_SaysSignInUnavailable()
    {
        var response = await CreateClient(configured: false).GetAsync("/api/me");
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.False(json.GetProperty("signInAvailable").GetBoolean());
    }

    [Fact]
    public async Task Login_WhenNotConfigured_Returns503()
    {
        var response = await CreateClient(configured: false).GetAsync("/auth/login");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Login_RedirectsToFencesWithPkceAndPinnedRedirectUri()
    {
        var response = await CreateClient(configured: true).GetAsync("/auth/login?returnUrl=/tracker");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.ToString();
        Assert.StartsWith(Authority + "/connect/authorize", location);
        Assert.Contains("client_id=farm", location);
        Assert.Contains("response_type=code", location);
        Assert.Contains("code_challenge=", location);
        Assert.Contains("code_challenge_method=S256", location);
        Assert.Contains("redirect_uri=" + Uri.EscapeDataString("https://eldervibe.dev/signin-oidc"), location);
        var query = System.Web.HttpUtility.ParseQueryString(response.Headers.Location.Query);
        Assert.Equal("openid profile email", query["scope"]);
    }

    [Fact]
    public async Task Logout_ClearsSessionAndSendsUserToFencesLogout()
    {
        var response = await CreateClient(configured: true).PostAsync("/auth/logout?returnUrl=/tracker", null);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var expectedBack = Uri.EscapeDataString("https://eldervibe.dev/tracker");
        Assert.Equal($"{Authority}/auth/logout?returnUrl={expectedBack}", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Logout_IsNotAvailableThroughGet()
    {
        var response = await CreateClient(configured: true).GetAsync("/auth/logout");

        Assert.NotEqual(HttpStatusCode.Redirect, response.StatusCode);
    }

    [Theory]
    [InlineData(null, "/")]
    [InlineData("", "/")]
    [InlineData("/tracker", "/tracker")]
    [InlineData("//evil.example", "/")]
    [InlineData("https://evil.example", "/")]
    [InlineData("/\\evil.example", "/")]
    public void ReturnUrl_AllowsOnlyLocalPaths(string? input, string expected)
    {
        Assert.Equal(expected, ReturnUrl.Sanitize(input));
    }

    [Fact]
    public async Task PortfolioEndpoints_StayAnonymous()
    {
        var response = await CreateClient(configured: true).GetAsync("/api/portfolio/profiles");

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Redirect, response.StatusCode);
    }
}
