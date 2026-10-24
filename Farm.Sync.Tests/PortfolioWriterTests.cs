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
        IEnumerable<SkillRow>? skills = null, string? kind = null) => new()
    {
        Id = id, Kind = kind, UpdatedAtUtc = DateTimeOffset.UnixEpoch, Metadata = [], Bookmarks = [],
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
    public async Task KindChangesSoftDeleteRowsOfTheOldKindAndRestoreThemWhenTheKindReturns()
    {
        var (connection, options) = await OpenAsync();
        await using var _ = connection;
        var project = new ProjectRow { SourceDocId = "page", Title = "Site", Slug = "site", Summary = "s", Skills = ["C#"] };
        var gig = new GigRow { SourceDocId = "page", Title = "Fix", Slug = "fix", Category = "Data", Summary = "s",
            Skills = ["C#"], Steps = [new() { StepNumber = 1, Title = "a" }] };
        var other = new ProjectRow { SourceDocId = "other", Title = "Other", Slug = "other", Summary = "s" };
        async Task Write(params SourceDocument[] documents)
        {
            await using var db = new PortfolioDbContext(options);
            await PortfolioWriter.WriteAsync(db, documents, Token);
        }

        await Write(Doc("p1", Profile(), kind: "Profile"), Doc("page", project: project, kind: "Project"),
            Doc("other", project: other, kind: "Project"));
        await Write(Doc("page", gig: gig, kind: "Gig"));
        await using (var read = new PortfolioDbContext(options))
        {
            Assert.Equal("other", (await read.Projects.SingleAsync()).Slug);
            var retired = await read.Projects.IgnoreQueryFilters().SingleAsync(p => p.SourceDocId == "page");
            Assert.NotNull(retired.DeletedAt);
            Assert.Equal(["other"], await read.ProfileProjects.Select(p => p.Project.Slug).ToListAsync());
            Assert.Empty(await read.ProjectSkills.ToListAsync());
            Assert.Equal("fix", (await read.GigProblems.SingleAsync()).Slug);
        }

        await Write(Doc("page", kind: ""));
        await using (var read = new PortfolioDbContext(options))
        {
            Assert.Empty(await read.GigProblems.ToListAsync());
            Assert.Empty(await read.GigSteps.ToListAsync());
            Assert.NotNull((await read.GigProblems.IgnoreQueryFilters().SingleAsync()).DeletedAt);
        }

        await Write(Doc("page", project: project, kind: "Project"));
        await using (var read = new PortfolioDbContext(options))
        {
            var restored = await read.Projects.Include(p => p.ProjectSkills).SingleAsync(p => p.SourceDocId == "page");
            Assert.Null(restored.DeletedAt);
            Assert.Single(restored.ProjectSkills);
            Assert.Equal(2, await read.ProfileProjects.CountAsync());
            Assert.Equal(2, await read.Projects.CountAsync());
            Assert.Empty(await read.GigProblems.ToListAsync());
        }
    }

    [Fact]
    public async Task ClearedKindIsReconciledWithoutRequiringAProfile()
    {
        var (connection, options) = await OpenAsync();
        await using var _ = connection;
        await using (var db = new PortfolioDbContext(options))
            await PortfolioWriter.WriteAsync(db, [Doc("untagged", kind: "")], Token);
        await using var read = new PortfolioDbContext(options);
        Assert.Empty(await read.Profiles.IgnoreQueryFilters().ToListAsync());
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
    public async Task FreeGigMarkdownRoundTripsWithoutLegacyFieldsAndReplacesOldSteps()
    {
        var (connection, options) = await OpenAsync();
        await using var _ = connection;
        var gig = new GigRow
        {
            SourceDocId = "free", Title = "Free", Slug = "free", Category = "Data", Summary = "s",
            Problem = "Old", Steps = [new() { StepNumber = 1, Title = "Old step" }]
        };
        await using (var db = new PortfolioDbContext(options))
            await PortfolioWriter.WriteAsync(db, [Doc("p1", Profile()), Doc("free", gig: gig)], Token);
        var body = "Intro\n\n## Arbitrary heading\n\n- List\n\n[Link](https://example.com)\n\n```cs\nvar x = 1;\n```\n" +
            new string('x', 12000);
        var free = gig with { BodyMarkdown = body, Problem = null, Steps = [] };
        var snapshot = new Snapshot
        {
            CapturedAtUtc = DateTimeOffset.Parse("2026-10-01T00:00:00Z"),
            Documents = [Doc("free", gig: free) with { UpdatedAtUtc = DateTimeOffset.Parse("2026-10-01T00:00:00Z") }]
        };
        snapshot.Validate();
        for (var i = 0; i < 2; i++)
        {
            await using var db = new PortfolioDbContext(options);
            await PortfolioWriter.WriteAsync(db, snapshot.Documents, Token);
        }
        await using var read = new PortfolioDbContext(options);
        var persisted = await read.GigProblems.SingleAsync();
        Assert.Equal(body, persisted.BodyMarkdown);
        using var report = new StringWriter();
        await GigBodyVerification.VerifyAsync(read, snapshot, report, Token);
        Assert.Contains("All 1 stored Gig BodyMarkdown values exactly match", report.ToString());
        Assert.DoesNotContain(body, report.ToString());
        Assert.DoesNotContain(read.ChangeTracker.Entries(), entry => entry.State != EntityState.Unchanged);
        var different = snapshot with
        {
            Documents = [snapshot.Documents[0] with { Gigs = [free with { BodyMarkdown = "Changed" }] }]
        };
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            GigBodyVerification.VerifyAsync(read, different, report, Token));
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            GigBodyVerification.VerifyAsync(read, snapshot with
            {
                Documents = [snapshot.Documents[0] with { Gigs = [free with { SourceDocId = "missing" }] }]
            }, report, Token));
        Assert.Null(persisted.Problem);
        Assert.Null(persisted.Outcome);
        Assert.Empty(await read.GigSteps.ToListAsync());
        Assert.Throws<InvalidDataException>(() => (snapshot with
        {
            Documents = [snapshot.Documents[0] with { Gigs = [free with { Category = "" }] }]
        }).Validate());
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
