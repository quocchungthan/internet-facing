using System.Security.Claims;
using System.Text.Json;
using Fences.Controllers;
using Fences.Data;
using Fences.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Fences.Tests;

public sealed class AudienceTests
{
    [Fact]
    public async Task AllClients_CountsPeopleAndDaysOnceAcrossApplications()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AudienceDbContext(new DbContextOptionsBuilder<AudienceDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var store = new AudienceStore(db);
        await store.RecordAsync("farm", "github:1", "owner", default);
        await store.RecordAsync("opensourcelab", "github:1", "owner", default);
        await store.RecordAsync("opensourcelab", "github:2", "friend", default);
        db.Visits.Add(new AudienceVisit { ClientId = "farm", Subject = "github:1", Username = "owner", Day = DateTime.UtcNow.AddDays(-10).ToString("yyyy-MM-dd") });
        await db.SaveChangesAsync();

        var data = JsonSerializer.SerializeToElement(await store.ReadAllAsync(7, default));
        Assert.Equal(2, data.GetProperty("totalUsers").GetInt32());
        Assert.Equal(1, data.GetProperty("newUsers").GetInt32());
        Assert.Equal(1, data.GetProperty("returningUsers").GetInt32());
        var owner = data.GetProperty("users").EnumerateArray().Single(row => row.GetProperty("subject").GetString() == "github:1");
        Assert.Equal(2, owner.GetProperty("observedDays").GetInt32());
        Assert.Equal(new[] { "farm", "opensourcelab" }, owner.GetProperty("clients").EnumerateArray().Select(value => value.GetString()));
        var apps = data.GetProperty("applications").EnumerateArray().ToDictionary(row => row.GetProperty("clientId").GetString()!, row => row.GetProperty("totalUsers").GetInt32());
        Assert.Equal(1, apps["farm"]);
        Assert.Equal(2, apps["opensourcelab"]);
    }

    [Fact]
    public async Task Observations_DeduplicateDailyAndKeepApplicationsSeparate()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AudienceDbContext(new DbContextOptionsBuilder<AudienceDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var store = new AudienceStore(db);
        await store.RecordAsync("farm", "github:1", "builder", default);
        await store.RecordAsync("farm", "github:1", "renamed-builder", default);
        await store.RecordAsync("farm", "github:3", "new-builder", default);
        await store.RecordAsync("other", "github:2", "other-builder", default);
        db.Visits.Add(new AudienceVisit { ClientId = "farm", Subject = "github:1", Username = "builder", Day = DateTime.UtcNow.AddDays(-10).ToString("yyyy-MM-dd") });
        await db.SaveChangesAsync();
        var data = JsonSerializer.SerializeToElement(await store.ReadAsync("farm", 7, default));
        Assert.Equal(2, data.GetProperty("totalUsers").GetInt32());
        Assert.Equal(1, data.GetProperty("newUsers").GetInt32());
        Assert.Equal(1, data.GetProperty("returningUsers").GetInt32());
        var user = data.GetProperty("users").EnumerateArray().Single(row => row.GetProperty("subject").GetString() == "github:1");
        Assert.Equal("renamed-builder", user.GetProperty("username").GetString());
        Assert.Equal(2, user.GetProperty("observedDays").GetInt32());
        Assert.Equal(1, data.GetProperty("growth")[0].GetProperty("users").GetInt32());
    }

    [Theory]
    [InlineData(null, 401)]
    [InlineData("123", 403)]
    [InlineData("44728650", 200)]
    public async Task AudienceEndpoint_OnlyOwnerCanRead(string? githubId, int status)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AudienceDbContext(new DbContextOptionsBuilder<AudienceDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var identity = githubId is null ? new ClaimsIdentity() : new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, githubId)], "test");
        var controller = new AudienceController(new AudienceStore(db), new ConfigurationBuilder().Build())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } }
        };
        var result = await controller.Get("farm", 7);
        var actual = result switch { StatusCodeResult code => code.StatusCode, ObjectResult obj => obj.StatusCode ?? 200, _ => 0 };
        Assert.Equal(status, actual);
        var allResult = await controller.GetAll(7);
        var allActual = allResult switch { StatusCodeResult code => code.StatusCode, ObjectResult obj => obj.StatusCode ?? 200, _ => 0 };
        Assert.Equal(status, allActual);
    }
}
