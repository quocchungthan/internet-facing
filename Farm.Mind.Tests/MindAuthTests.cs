using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Farm.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Farm.Mind.Tests;

public class MindAuthTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Issuer = "https://identity.test";
    private static readonly SymmetricSecurityKey SigningKey = new(new byte[32].Select((_, i) => (byte)(i + 1)).ToArray());

    private readonly WebApplicationFactory<Program> _factory;

    public MindAuthTests(WebApplicationFactory<Program> factory) => _factory = factory;

    private HttpClient CreateClient(bool configured = true) => _factory.WithWebHostBuilder(builder =>
    {
        builder.UseSetting("FarmAuth:Authority", configured ? Issuer : "");
        builder.UseSetting("FarmAuth:ClientId", configured ? "farm" : "");
        builder.UseSetting("FarmAuth:PublicBaseUrl", "https://eldervibe.dev");
        builder.ConfigureServices(services =>
        {
            // Use a local signing key instead of fetching discovery metadata from the issuer.
            services.PostConfigure<JwtBearerOptions>(FarmAuthSchemes.Bearer, options =>
            {
                options.Authority = null;
                options.MetadataAddress = null!;
                options.Configuration = new OpenIdConnectConfiguration { Issuer = Issuer, SigningKeys = { SigningKey } };
                options.TokenValidationParameters.ValidIssuer = Issuer;
                options.TokenValidationParameters.IssuerSigningKey = SigningKey;
            });
        });
    }).CreateClient();

    private static string Token(string audience = "farm-mind", string subject = "github:42")
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = audience,
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256),
            Claims = new Dictionary<string, object> { ["sub"] = subject, ["name"] = "Test User" }
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    [Fact]
    public async Task Healthz_IsAnonymous()
    {
        var response = await CreateClient().GetAsync("/healthz");
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task ProtectedResourceMetadata_PointsAtFences()
    {
        var response = await CreateClient().GetAsync("/.well-known/oauth-protected-resource");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("https://eldervibe.dev/mcp", json.GetProperty("resource").GetString());
        Assert.Equal(Issuer, json.GetProperty("authorization_servers")[0].GetString());
    }

    [Fact]
    public async Task ProtectedResourceMetadata_HasPathVariantForMcp()
    {
        var response = await CreateClient().GetAsync("/.well-known/oauth-protected-resource/mcp");
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Mcp_WithoutToken_Returns401WithResourceMetadataHint()
    {
        var response = await CreateClient().PostAsync("/mcp", new StringContent("{}"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var challenge = Assert.Single(response.Headers.WwwAuthenticate);
        Assert.Equal("Bearer", challenge.Scheme);
        Assert.Contains("resource_metadata=\"https://eldervibe.dev/.well-known/oauth-protected-resource\"", challenge.Parameter);
    }

    [Fact]
    public async Task Mcp_WithValidToken_ReachesPlaceholder()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token());

        var response = await client.PostAsync("/mcp", new StringContent("{}"));

        Assert.Equal(HttpStatusCode.NotImplemented, response.StatusCode);
        Assert.Contains("github:42", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Mcp_WithWrongAudience_Returns401()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(audience: "someone-else"));

        var response = await client.PostAsync("/mcp", new StringContent("{}"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MemoryApi_WithoutCredentials_Returns401NotRedirect()
    {
        var response = await CreateClient().GetAsync("/api/memory/whoami");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task MemoryApi_WithBearerToken_ReturnsSubject()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token());

        var response = await client.GetAsync("/api/memory/whoami");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("github:42", json.GetProperty("subject").GetString());
    }
}
