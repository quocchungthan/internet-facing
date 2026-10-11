using AffineSync;
using Farm.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AffineSync.Tests;

public sealed class PortfolioWriterTests
{
    private static readonly CancellationToken Token = CancellationToken.None;

    private static SourceDocument Doc(string id, ProfileRow? profile = null, ProjectRow? project = null, GigRow? gig = null,
        IEnumerable<SkillRow>? skills = null) => new()
    {
        Id = id, UpdatedAtUtc = DateTimeOffset.UnixEpoch, Metadata = [], Bookmarks = [],
        Profiles = profile is null ? [] : [profile], Projects = project is null ? [] : [project],
        Gigs = gig is null ? [] : [gig], Skills = skills?.ToList() ?? []
    };

    private static ProfileRow Profile() => new() { SourceDocId = "p1", DisplayName = "Kai", Headline = "Dev", Bio = "Hello" };

    private static async Task<(SqliteConnection, DbContextOptions<PortfolioDbContext>)> OpenAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<PortfolioDbContext>().UseSqlite(connection).Options;
        await using var db = new PortfolioDbContext(options);
        await db.Database.EnsureCreatedAsync();
        return (connection, options);
    }

    [Fact]
    public async Task ReplaysAreIdempotentAndBodiesAndStepsAreWritten()
    {
        var (connection, options) = await OpenAsync();
        await using var _ = connection;
        var gig = new GigRow
        {
            SourceDocId = "g1", Title = "Fix", Slug = "fix", Category = "Data", Summary = "s", Problem = "broken",
            Outcome = "fixed", Skills = ["EF Core"],
            Steps = [new() { StepNumber = 1, Title = "a", Description = "da" }, new() { StepNumber = 2, Title = "b" }]
        };
        var project = new ProjectRow
        {
            SourceDocId = "j1", Title = "Site", Slug = "site", Summary = "s", Description = "body", Skills = ["C#"]
        };
        var docs = new[] { Doc("p1", Profile()), Doc("j1", project: project), Doc("g1", gig: gig) };
        for (var i = 0; i < 2; i++)
        {
            await using var db = new PortfolioDbContext(options);
            await PortfolioWriter.WriteAsync(db, docs, Token);
        }

        await using var read = new PortfolioDbContext(options);
        Assert.Equal("Hello", (await read.Profiles.SingleAsync()).Bio);
        Assert.Equal("body", (await read.Projects.SingleAsync()).Description);
        Assert.Equal(2, await read.GigSteps.CountAsync());
        Assert.Equal(2, await read.Skills.CountAsync());
        Assert.Single(await read.ProjectSkills.ToListAsync());
        Assert.Single(await read.GigProblemSkills.ToListAsync());
    }

    [Fact]
    public async Task RemovedStepsAndSkillsAreReplacedButEntitiesAreKept()
    {
        var (connection, options) = await OpenAsync();
        await using var _ = connection;
        GigRow Gig(IEnumerable<GigStepRow> steps, params string[] skills) => new()
        {
            SourceDocId = "g1", Title = "Fix", Slug = "fix", Category = "Data", Summary = "s", Skills = [.. skills], Steps = [.. steps]
        };
        await using (var db = new PortfolioDbContext(options))
            await PortfolioWriter.WriteAsync(db, [Doc("p1", Profile()), Doc("g1", gig: Gig(
                [new() { StepNumber = 1, Title = "a" }, new() { StepNumber = 2, Title = "b" }], "X", "Y"))], Token);
        await using (var db = new PortfolioDbContext(options))
            await PortfolioWriter.WriteAsync(db, [Doc("g1", gig: Gig([new() { StepNumber = 1, Title = "a2" }], "X"))], Token);

        await using var read = new PortfolioDbContext(options);
        Assert.Equal("a2", (await read.GigSteps.SingleAsync()).Title);
        Assert.Single(await read.GigProblemSkills.ToListAsync());
        Assert.Equal(2, await read.Skills.CountAsync());
        Assert.Single(await read.GigProblems.ToListAsync());
    }

    [Fact]
    public async Task ExistingRowWithoutSourceDocIsAdoptedBySlug()
    {
        var (connection, options) = await OpenAsync();
        await using var _ = connection;
        await using (var db = new PortfolioDbContext(options))
        {
            db.Profiles.Add(new() { DisplayName = "Old", Headline = "h" });
            await db.SaveChangesAsync();
            db.Projects.Add(new() { Title = "Seed", Slug = "site", Summary = "s" });
            await db.SaveChangesAsync();
        }
        var project = new ProjectRow { SourceDocId = "j1", Title = "Site", Slug = "site", Summary = "s2" };
        await using (var db = new PortfolioDbContext(options))
            await PortfolioWriter.WriteAsync(db, [Doc("p1", Profile()), Doc("j1", project: project)], Token);

        await using var read = new PortfolioDbContext(options);
        var row = await read.Projects.SingleAsync();
        Assert.Equal("j1", row.SourceDocId);
        Assert.Equal("Site", row.Title);
        Assert.Equal("Kai", (await read.Profiles.SingleAsync()).DisplayName);
    }

    [Fact]
    public async Task ChangedPageWithoutProfileAttachesToExistingProfileOrFails()
    {
        var (connection, options) = await OpenAsync();
        await using var _ = connection;
        var project = new ProjectRow { SourceDocId = "j1", Title = "Site", Slug = "site", Summary = "s" };
        await using (var db = new PortfolioDbContext(options))
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                PortfolioWriter.WriteAsync(db, [Doc("j1", project: project)], Token));
        await using (var db = new PortfolioDbContext(options))
            await PortfolioWriter.WriteAsync(db, [Doc("p1", Profile())], Token);
        await using (var db = new PortfolioDbContext(options))
            await PortfolioWriter.WriteAsync(db, [Doc("j1", project: project)], Token);
        await using var read = new PortfolioDbContext(options);
        Assert.Single(await read.ProfileProjects.ToListAsync());
    }
}
