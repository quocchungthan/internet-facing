using System.Linq.Expressions;
using System.Reflection;
using Farm.Controllers;
using Farm.Data;
using Farm.Data.Entities;
using Farm.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace Farm.Tests;

public class PortfolioModelTests
{
    private static PortfolioDbContext CreateContext() => new(new DbContextOptionsBuilder<PortfolioDbContext>()
        .UseNpgsql("Host=localhost;Database=portfolio_model_tests;Username=unused",
            provider => provider.MigrationsHistoryTable("__PortfolioMigrationsHistory")).Options);

    [Fact]
    public void Model_ContainsOnlyElevenEntitiesAndNoSeeds()
    {
        using var db = CreateContext();
        var model = db.GetService<IDesignTimeModel>().Model;
        Assert.Equal(11, model.GetEntityTypes().Count());
        Assert.All(model.GetEntityTypes(), entity => Assert.Empty(entity.GetSeedData()));
        Assert.DoesNotContain(model.GetEntityTypes(), e => e.ClrType == typeof(PlatformBookmark));
        Assert.Equal("text", model.FindEntityType(typeof(PortfolioProfile))!
            .FindProperty(nameof(PortfolioProfile.AvatarSvg))!.GetColumnType());
    }

    [Theory]
    [InlineData(typeof(PortfolioProfile), "DisplayName", 160, false)]
    [InlineData(typeof(PortfolioProfile), "Headline", 240, false)]
    [InlineData(typeof(PortfolioProfile), "Bio", 4000, true)]
    [InlineData(typeof(PortfolioProfile), "Location", 160, true)]
    [InlineData(typeof(PortfolioProfile), "ContactEmail", 320, true)]
    [InlineData(typeof(PortfolioProfile), "ProfileImageUrl", 2048, true)]
    [InlineData(typeof(PortfolioProfile), "ResumeUrl", 2048, true)]
    [InlineData(typeof(PortfolioProject), "Title", 200, false)]
    [InlineData(typeof(PortfolioProject), "Slug", 220, false)]
    [InlineData(typeof(PortfolioProject), "Summary", 500, false)]
    [InlineData(typeof(PortfolioProject), "Description", 8000, true)]
    [InlineData(typeof(PortfolioProject), "DemoUrl", 2048, true)]
    [InlineData(typeof(PortfolioProject), "SourceUrl", 2048, true)]
    [InlineData(typeof(PortfolioExperience), "Company", 200, false)]
    [InlineData(typeof(PortfolioExperience), "JobTitle", 160, false)]
    [InlineData(typeof(PortfolioExperience), "Description", 4000, true)]
    [InlineData(typeof(PortfolioExperience), "EmploymentType", 20, false)]
    [InlineData(typeof(PortfolioExperience), "WorkMode", 20, false)]
    [InlineData(typeof(PortfolioSkill), "Name", 100, false)]
    [InlineData(typeof(PortfolioSkill), "Category", 80, false)]
    [InlineData(typeof(PortfolioSocialLink), "Label", 80, false)]
    [InlineData(typeof(PortfolioSocialLink), "Url", 2048, false)]
    [InlineData(typeof(ProfileProject), "Role", 160, true)]
    [InlineData(typeof(ProfileProject), "Organization", 200, true)]
    public void SourceLengthsAndNullability_ArePreserved(Type type, string name, int length, bool nullable)
    {
        using var db = CreateContext();
        var property = db.Model.FindEntityType(type)!.FindProperty(name)!;
        Assert.Equal(length, property.GetMaxLength());
        Assert.Equal(nullable, property.IsNullable);
    }

    [Theory]
    [InlineData(typeof(ProfileProject), "ProfileId", "ProjectId")]
    [InlineData(typeof(ProfileSkill), "ProfileId", "SkillId")]
    [InlineData(typeof(ProjectSkill), "ProjectId", "SkillId")]
    public void Associations_KeepCompositeKeys(Type type, string first, string second)
    {
        using var db = CreateContext();
        Assert.Equal(new[] { first, second },
            db.Model.FindEntityType(type)!.FindPrimaryKey()!.Properties.Select(p => p.Name));
    }

    [Fact]
    public void IndexesRelationshipsDatesAndStringEnums_MatchSource()
    {
        using var db = CreateContext();
        var model = db.Model;
        Assert.Contains(model.FindEntityType(typeof(PortfolioProject))!.GetIndexes(),
            i => i.IsUnique && i.Properties.Single().Name == "Slug");
        Assert.Contains(model.FindEntityType(typeof(PortfolioSkill))!.GetIndexes(),
            i => i.IsUnique && i.Properties.Single().Name == "Name");
        Assert.Contains(model.FindEntityType(typeof(PortfolioSocialLink))!.GetIndexes(),
            i => i.IsUnique && i.Properties.Select(p => p.Name).SequenceEqual(new[] { "ProfileId", "Label" }));
        Assert.Contains(model.FindEntityType(typeof(ProfileProject))!.GetIndexes(),
            i => i.Properties.Select(p => p.Name).SequenceEqual(new[] { "ProfileId", "IsFeatured", "DisplayOrder" }));
        foreach (var type in new[] { typeof(PortfolioExperience), typeof(ProfileSkill), typeof(PortfolioSocialLink) })
            Assert.Contains(model.FindEntityType(type)!.GetIndexes(),
                i => i.Properties.Select(p => p.Name).SequenceEqual(new[] { "ProfileId", "DisplayOrder" }));

        var foreignKeys = model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()).ToArray();
        Assert.Equal(11, foreignKeys.Length);
        Assert.All(foreignKeys, key =>
        {
            var skillRestriction = (key.DeclaringEntityType.ClrType == typeof(ProjectSkill)
                || key.DeclaringEntityType.ClrType == typeof(GigProblemSkill))
                && key.PrincipalEntityType.ClrType == typeof(PortfolioSkill);
            Assert.Equal(skillRestriction ? DeleteBehavior.NoAction : DeleteBehavior.Cascade, key.DeleteBehavior);
            Assert.True(key.IsRequired);
        });
        foreach (var type in new[] { typeof(PortfolioExperience), typeof(ProfileProject) })
        {
            var entity = model.FindEntityType(type)!;
            Assert.Equal("date", entity.FindProperty("StartDate")!.GetColumnType());
            Assert.Equal("date", entity.FindProperty("EndDate")!.GetColumnType());
            Assert.True(entity.FindProperty("EndDate")!.IsNullable);
        }
        var experience = model.FindEntityType(typeof(PortfolioExperience))!;
        Assert.False(experience.FindProperty("StartDate")!.IsNullable);
        Assert.Equal("Contract", experience.FindProperty("EmploymentType")!.GetTypeMapping()
            .Converter!.ConvertToProvider(EmploymentType.Contract));
        Assert.Equal("Remote", experience.FindProperty("WorkMode")!.GetTypeMapping()
            .Converter!.ConvertToProvider(WorkMode.Remote));
    }

    [Fact]
    public void MigrationSnapshotAndPostgresSql_AreConsistent_WithoutConnecting()
    {
        using var db = CreateContext();
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Equal(2, db.Database.GetMigrations().Count());
        var script = db.GetService<IMigrator>().GenerateScript();
        Assert.Contains("__PortfolioMigrationsHistory", script);
        Assert.DoesNotContain("INSERT INTO \"Portfolio", script);
        Assert.DoesNotContain("WebsiteMetadata", script);
        Assert.DoesNotContain("nvarchar", script);
        var tables = db.GetService<IDesignTimeModel>().Model.GetEntityTypes().ToArray();
        Assert.All(tables, entity => Assert.Contains($"CREATE TABLE \"{entity.GetTableName()}\"", script));
        var constraints = tables.SelectMany(e => e.GetCheckConstraints()).ToArray();
        Assert.Equal(9, constraints.Length);
        Assert.All(constraints, constraint => Assert.Contains(constraint.Sql, script));
        Assert.Contains("NOT \"IsCurrent\" OR \"EndDate\" IS NULL", script);
        Assert.Contains("ON DELETE CASCADE", script);
    }

    [Theory]
    [InlineData("Profiles")]
    [InlineData("Projects")]
    [InlineData("Skills")]
    [InlineData("Experiences")]
    [InlineData("SocialLinks")]
    [InlineData("ProfileProjects")]
    [InlineData("ProfileSkills")]
    [InlineData("ProjectSkills")]
    public void RawQueries_TranslateFiltersOrderingAndPagingOnPostgres(string property)
    {
        using var db = CreateContext();
        var controller = new PortfolioController(db, new ClientTracingService());
        // Inspect the controller's actual projections, not a duplicate query in the test.
        var query = (IQueryable)typeof(PortfolioController)
            .GetProperty(property, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(controller)!;
        var method = typeof(PortfolioModelTests).GetMethod(nameof(QuerySql),
            BindingFlags.Static | BindingFlags.NonPublic)!.MakeGenericMethod(query.ElementType);
        var sql = (string)method.Invoke(null, new object[] { query })!;
        Assert.Contains("WHERE", sql);
        Assert.Contains("ORDER BY", sql);
        Assert.Contains("LIMIT", sql);
        Assert.Contains("OFFSET", sql);
        Assert.DoesNotContain("JOIN", sql);
        Assert.DoesNotContain("ContactEmail", sql);
    }

    private static string QuerySql<T>(IQueryable<T> query)
    {
        var parameter = Expression.Parameter(typeof(T), "row");
        var keys = typeof(T).GetProperty("Id") is { } id
            ? new[] { id } : typeof(T).GetProperties().Where(p => p.Name.EndsWith("Id")).ToArray();
        var condition = keys.Select(key => (Expression)Expression.Equal(
            Expression.Property(parameter, key), Expression.Constant(1))).Aggregate(Expression.AndAlso);
        query = query.Where(Expression.Lambda<Func<T, bool>>(condition, parameter));
        IOrderedQueryable<T>? ordered = null;
        foreach (var key in keys)
        {
            var selector = Expression.Lambda<Func<T, int>>(Expression.Property(parameter, key), parameter);
            ordered = ordered is null ? query.OrderBy(selector) : ordered.ThenBy(selector);
        }
        return ordered!.Skip(50).Take(50).ToQueryString();
    }
}
