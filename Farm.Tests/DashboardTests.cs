using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Farm.Tests;

public class DashboardTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    static DashboardTests() => Environment.SetEnvironmentVariable("USE_IN_MEMORY_DATABASE", "true");

    public DashboardTests(WebApplicationFactory<Program> factory) => _factory = factory;

    private HttpClient Client(string? subject = null)
    {
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Dashboard:OwnerSubjects:0", "github:44728650");
            builder.ConfigureServices(services => services
                .AddAuthentication(options => options.DefaultAuthenticateScheme = "DashboardTest")
                .AddScheme<AuthenticationSchemeOptions, DashboardTestAuth>("DashboardTest", _ => { }));
        });
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        if (subject is not null) client.DefaultRequestHeaders.Add("X-Test-Subject", subject);
        return client;
    }

    [Fact]
    public async Task Anonymous_CannotReadAnalytics()
    {
        var response = await Client().GetAsync("/api/dashboard");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(7, 6)]
    [InlineData(30, 26)]
    [InlineData(90, 78)]
    public async Task RegularUser_GetsLabeledDemoWithinRange_WithoutAudience(int days, int expectedTasks)
    {
        var response = await Client("github:123").GetAsync($"/api/dashboard?days={days}");
        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.CacheControl?.NoStore);
        var data = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("demo", data.GetProperty("source").GetString());
        Assert.False(data.GetProperty("isOwner").GetBoolean());
        Assert.Equal(JsonValueKind.Null, data.GetProperty("audience").ValueKind);
        var tasks = data.GetProperty("tasks").EnumerateArray().ToArray();
        Assert.Equal(expectedTasks, tasks.Length);
        var from = DateOnly.Parse(data.GetProperty("from").GetString()!);
        var through = DateOnly.Parse(data.GetProperty("through").GetString()!);
        Assert.Equal(days - 1, through.DayNumber - from.DayNumber);
        foreach (var task in tasks)
        {
            Assert.InRange(DateOnly.Parse(task.GetProperty("date").GetString()!), from, through);
            Assert.True(task.GetProperty("inputTokens").GetInt32() > 0);
            Assert.True(task.GetProperty("outputTokens").GetInt32() > 0);
            Assert.True(task.GetProperty("durationSeconds").GetInt32() > 0);
            Assert.True(task.GetProperty("inputBytes").GetInt32() > 0);
            Assert.True(task.GetProperty("outputBytes").GetInt32() > 0);
        }
        if (days == 7)
        {
            Assert.Equal(1.52m, tasks.Sum(task => task.GetProperty("costUsd").GetDecimal()));
            Assert.Equal(0.06m, tasks.Sum(task => task.GetProperty("feeUsd").GetDecimal()));
            Assert.Equal(2.736m, tasks.Sum(task => task.GetProperty("baselineCostUsd").GetDecimal()));
        }
    }

    [Fact]
    public async Task Owner_GetsFencesSourceWithoutFictionalUsers()
    {
        var data = await Client("github:44728650").GetFromJsonAsync<JsonElement>("/api/dashboard");
        Assert.True(data.GetProperty("isOwner").GetBoolean());
        var audience = data.GetProperty("audience");
        Assert.Equal("fences", audience.GetProperty("source").GetString());
        Assert.False(audience.TryGetProperty("users", out _));
        Assert.Equal(JsonValueKind.Null, audience.GetProperty("url").ValueKind);
    }

    [Fact]
    public async Task OwnerDisplayNameAndEmail_DoNotGrantAudienceAccess()
    {
        var client = Client("github:123");
        client.DefaultRequestHeaders.Add("X-Test-Username", "quocchungthan");
        client.DefaultRequestHeaders.Add("X-Test-Email", "owner@example.test");
        var data = await client.GetFromJsonAsync<JsonElement>("/api/dashboard");
        Assert.False(data.GetProperty("isOwner").GetBoolean());
        Assert.Equal(JsonValueKind.Null, data.GetProperty("audience").ValueKind);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    [InlineData(365)]
    public async Task UnsupportedRange_IsRejected(int days)
    {
        var response = await Client("github:123").GetAsync($"/api/dashboard?days={days}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private sealed class DashboardTestAuth(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var subject = Request.Headers["X-Test-Subject"].ToString();
            if (string.IsNullOrEmpty(subject)) return Task.FromResult(AuthenticateResult.NoResult());
            var claims = new[]
            {
                new Claim("sub", subject),
                new Claim("preferred_username", Request.Headers["X-Test-Username"].ToString()),
                new Claim("email", Request.Headers["X-Test-Email"].ToString())
            };
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
    }
}
