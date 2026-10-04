using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Farm.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Farm.Tests;

public class ApiEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    static ApiEndpointsTests()
    {
        Environment.SetEnvironmentVariable("USE_IN_MEMORY_DATABASE", "true");
    }

    private readonly WebApplicationFactory<Program> _factory;

    public ApiEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetMetadata_Returns_SeededDeveloperMetadata()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/metadata");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("quocchungthan", json.GetProperty("developerName").GetString());
        Assert.Equal("a Elder engineer learn to vibe code", json.GetProperty("title").GetString());
        Assert.Equal("if you are seeking for a software developer to talk to, it's me here", json.GetProperty("description").GetString());
        Assert.Equal("https://github.com/quocchungthan/internet-facing", json.GetProperty("repositoryUrl").GetString());

        // Check tracing headers
        Assert.True(response.Headers.Contains("X-Total-Requests"));
        Assert.True(response.Headers.Contains("X-Unique-Clients"));
        Assert.True(response.Headers.Contains("X-RateLimit-Limit"));
        Assert.True(response.Headers.Contains("X-RateLimit-Remaining"));
    }

    [Fact]
    public async Task GetFeeds_Returns_SeededPlatformBookmarks()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/feeds");
        response.EnsureSuccessStatusCode();

        var feeds = await response.Content.ReadFromJsonAsync<List<JsonElement>>();
        Assert.NotNull(feeds);
        Assert.True(feeds.Count >= 6);

        var platforms = feeds.Select(f => f.GetProperty("platform").GetString()).ToList();
        Assert.Contains("GitHub", platforms);
        Assert.Contains("TikTok", platforms);
        Assert.Contains("Facebook", platforms);
        Assert.Contains("Instagram", platforms);
        Assert.Contains("Azure DevOps", platforms);
        Assert.Contains("Gmail", platforms);

        var github = feeds.First(f => f.GetProperty("platform").GetString() == "GitHub");
        Assert.Equal("#238636", github.GetProperty("color").GetString());
    }

    [Fact]
    public async Task GetMetrics_Returns_ValidTracingNumbers()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/metrics");
        response.EnsureSuccessStatusCode();

        var metrics = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(metrics.TryGetProperty("totalRequests", out _));
        Assert.True(metrics.TryGetProperty("uniqueClients", out _));
        Assert.True(metrics.TryGetProperty("currentClient", out _));
    }

    [Fact]
    public async Task RateLimiting_Returns_429_WhenLimitExceeded()
    {
        var client = _factory.CreateClient();
        var clientId = "test-rate-limit-client-" + Guid.NewGuid();
        client.DefaultRequestHeaders.Add("X-Client-Id", clientId);

        HttpResponseMessage? lastResponse = null;
        for (int i = 0; i < 65; i++)
        {
            lastResponse = await client.GetAsync("/api/metadata");
            if (lastResponse.StatusCode == HttpStatusCode.TooManyRequests)
            {
                break;
            }
        }

        Assert.NotNull(lastResponse);
        Assert.Equal(HttpStatusCode.TooManyRequests, lastResponse.StatusCode);
        Assert.True(lastResponse.Headers.Contains("X-Client-Status"));
        Assert.Equal("RateLimitExceeded", lastResponse.Headers.GetValues("X-Client-Status").First());
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/winchester")]
    [InlineData("/tracker")]
    [InlineData("/brick")]
    public async Task DirectRouteAccess_Returns_Success(string route)
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync(route);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("<div id=\"root\"></div>", content);
    }
}
