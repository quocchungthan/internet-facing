using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Farm.Controllers;
using Farm.Data;
using Farm.Data.Entities;
using Farm.Models;
using Farm.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Farm.Tests;

public sealed class PortfolioApiFixture : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["UseInMemoryDatabase"] = "true",
                ["InMemoryDatabaseName"] = "PortfolioTests-" + Guid.NewGuid()
            }));
    }

    public PortfolioApiFixture()
    {
        // TestServer is in-process; no PostgreSQL, services or database migrations are run.
        using var client = CreateClient();
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PortfolioDbContext>();
        db.Profiles.AddRange(Enumerable.Range(1, 105).Reverse().Select(id => new PortfolioProfile
        {
            Id = id, DisplayName = $"Test profile {id}", Headline = "Test headline",
            ContactEmail = "private@example.invalid", AvatarSvg = "<svg/>"
        }));
        for (var id = 3; id >= 1; id--)
        {
            db.Projects.Add(new PortfolioProject
                { Id = id, Title = $"Project {id}", Slug = $"test-{id}", Summary = "Test" });
            db.Skills.Add(new PortfolioSkill { Id = id, Name = $"Skill {id}", Category = "Test" });
            db.Experiences.Add(new PortfolioExperience
            {
                Id = id, ProfileId = 1, Company = "Test company", JobTitle = "Test role",
                EmploymentType = EmploymentType.Contract, WorkMode = WorkMode.Remote,
                StartDate = new DateOnly(2025, 1, 1), IsCurrent = true
            });
            db.SocialLinks.Add(new PortfolioSocialLink
                { Id = id, ProfileId = 1, Label = $"Test {id}", Url = "https://example.invalid" });
            db.ProfileProjects.Add(new ProfileProject { ProfileId = 1, ProjectId = id });
            db.ProfileSkills.Add(new ProfileSkill { ProfileId = 1, SkillId = id });
            db.ProjectSkills.Add(new ProjectSkill { ProjectId = 1, SkillId = id });
        }
        db.ProfileProjects.Add(new ProfileProject { ProfileId = 2, ProjectId = 1 });
        db.ProfileSkills.Add(new ProfileSkill { ProfileId = 2, SkillId = 1 });
        db.ProjectSkills.Add(new ProjectSkill { ProjectId = 2, SkillId = 1 });
        db.SaveChanges();
    }

    public HttpClient NewClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Client-Id", Guid.NewGuid().ToString());
        return client;
    }
}

public class PortfolioApiTests(PortfolioApiFixture fixture) : IClassFixture<PortfolioApiFixture>
{
    public static TheoryData<string, string> Routes => new()
    {
        { "profiles", "1" }, { "projects", "1" }, { "skills", "1" },
        { "experiences", "1" }, { "social-links", "1" },
        { "profile-projects", "1/1" }, { "profile-skills", "1/1" }, { "project-skills", "1/1" }
    };

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task CollectionsAndKeys_ReturnFlatRows(string route, string key)
    {
        using var client = fixture.NewClient();
        var collection = await client.GetAsync($"/api/portfolio/{route}?pageSize=1");
        Assert.Equal(HttpStatusCode.OK, collection.StatusCode);
        Assert.True(collection.Headers.Contains("X-RateLimit-Limit"));
        var rows = await collection.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, rows.GetArrayLength());
        var response = await client.GetAsync($"/api/portfolio/{route}/{key}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var row = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(rows[0].GetRawText(), row.GetRawText());
        Assert.All(row.EnumerateObject(), property =>
            Assert.DoesNotContain(property.Value.ValueKind, new[] { JsonValueKind.Array, JsonValueKind.Object }));
        Assert.False(row.TryGetProperty("contactEmail", out _));
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task MissingKeysAndEmptyPages_Return404AndEmptyArray(string route, string key)
    {
        using var client = fixture.NewClient();
        var missing = key.Contains('/') ? "9999/9999" : "9999";
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/portfolio/{route}/{missing}")).StatusCode);
        if (key.Contains('/'))
            Assert.Equal(HttpStatusCode.NotFound,
                (await client.GetAsync($"/api/portfolio/{route}/1/9999")).StatusCode);
        var rows = await client.GetFromJsonAsync<JsonElement>($"/api/portfolio/{route}?page=10000");
        Assert.Equal(0, rows.GetArrayLength());
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("page=-1")]
    [InlineData("page=10001")]
    [InlineData("page=2147483647")]
    [InlineData("page=bad")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=-1")]
    [InlineData("pageSize=101")]
    [InlineData("pageSize=bad")]
    public async Task InvalidPaging_Returns400(string query)
    {
        using var client = fixture.NewClient();
        var response = await client.GetAsync("/api/portfolio/profiles?" + query);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task InvalidKeysAndWriteMethods_AreRejected(string route, string key)
    {
        using var client = fixture.NewClient();
        var invalid = key.Contains('/') ? "0/1" : "0";
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.GetAsync($"/api/portfolio/{route}/{invalid}")).StatusCode);
        foreach (var method in new[] { HttpMethod.Post, HttpMethod.Put, HttpMethod.Patch, HttpMethod.Delete })
        {
            foreach (var path in new[] { route, $"{route}/{key}" })
            {
                using var request = new HttpRequestMessage(method, "/api/portfolio/" + path);
                var response = await client.SendAsync(request);
                Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
            }
        }
    }

    [Fact]
    public async Task Paging_IsBoundedAndDeterministic_AndPrivateContactIsAbsent()
    {
        using var client = fixture.NewClient();
        var defaults = await client.GetFromJsonAsync<JsonElement>("/api/portfolio/profiles");
        Assert.Equal(50, defaults.GetArrayLength());
        var max = await client.GetFromJsonAsync<JsonElement>("/api/portfolio/profiles?pageSize=100");
        Assert.Equal(100, max.GetArrayLength());
        var second = await client.GetFromJsonAsync<JsonElement>("/api/portfolio/profiles?page=2&pageSize=100");
        Assert.Equal(5, second.GetArrayLength());
        Assert.Equal(101, second[0].GetProperty("id").GetInt32());
        var repeat = await client.GetFromJsonAsync<JsonElement>("/api/portfolio/profiles?page=2&pageSize=100");
        Assert.Equal(second.GetRawText(), repeat.GetRawText());
        Assert.DoesNotContain("contactEmail", max.GetRawText());
        Assert.DoesNotContain("private@example.invalid", max.GetRawText());
        Assert.Equal("<svg/>", max[0].GetProperty("avatarSvg").GetString());
    }

    [Theory]
    [InlineData("profile-projects", "profileId", "projectId")]
    [InlineData("profile-skills", "profileId", "skillId")]
    [InlineData("project-skills", "projectId", "skillId")]
    public async Task AssociationPaging_OrdersByBothKeys(string route, string first, string second)
    {
        using var client = fixture.NewClient();
        var rows = await client.GetFromJsonAsync<JsonElement>($"/api/portfolio/{route}?page=2&pageSize=2");
        Assert.Equal(1, rows[0].GetProperty(first).GetInt32());
        Assert.Equal(3, rows[0].GetProperty(second).GetInt32());
        Assert.Equal(2, rows[1].GetProperty(first).GetInt32());
        Assert.Equal(1, rows[1].GetProperty(second).GetInt32());
    }

    [Fact]
    public async Task Experience_PreservesDatesEnumsAndNullability()
    {
        using var client = fixture.NewClient();
        var row = await client.GetFromJsonAsync<JsonElement>("/api/portfolio/experiences/1");
        Assert.Equal("2025-01-01", row.GetProperty("startDate").GetString());
        Assert.Equal(JsonValueKind.Null, row.GetProperty("endDate").ValueKind);
        Assert.Equal((int)EmploymentType.Contract, row.GetProperty("employmentType").GetInt32());
        Assert.Equal((int)WorkMode.Remote, row.GetProperty("workMode").GetInt32());
    }

    [Fact]
    public async Task RateLimit_IsSharedWithExistingApi()
    {
        using var client = fixture.NewClient();
        HttpResponseMessage? response = null;
        for (var i = 0; i < 61; i++)
            response = await client.GetAsync(i % 2 == 0 ? "/api/portfolio/profiles/1" : "/api/metadata");
        Assert.NotNull(response);
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    [Fact]
    public async Task Reads_DoNotTrackEntities_AndHonorCancellation()
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PortfolioDbContext>();
        var controller = new PortfolioController(db, new ClientTracingService())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext(),
                ActionDescriptor = new ControllerActionDescriptor
                {
                    AttributeRouteInfo = new Microsoft.AspNetCore.Mvc.Routing.AttributeRouteInfo
                        { Template = "api/portfolio/profiles" }
                }
            }
        };
        Assert.IsType<OkObjectResult>(await controller.GetProfiles(new PortfolioPage(), default));
        Assert.IsType<OkObjectResult>(await controller.GetProfile(1, default));
        Assert.Empty(db.ChangeTracker.Entries());
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            controller.GetProfiles(new PortfolioPage(), cancelled.Token));
        Assert.Empty(db.ChangeTracker.Entries());
    }
}
