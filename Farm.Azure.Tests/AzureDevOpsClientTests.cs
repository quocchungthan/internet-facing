using Xunit;

namespace Farm.Azure.Tests;

public sealed class AzureDevOpsClientTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetWorkItemsAsync_rejects_non_positive_ids(int id)
    {
        IAzureDevOpsClient client = CreateClient();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => client.GetWorkItemsAsync([id]));

        Assert.Contains("positive", exception.Message);
    }

    [Fact]
    public async Task GetWorkItemsAsync_rejects_duplicate_ids()
    {
        IAzureDevOpsClient client = CreateClient();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => client.GetWorkItemsAsync([1, 1]));

        Assert.Contains("duplicates", exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetWorkItemsAssignedToAsync_rejects_blank_assignee(string assignee)
    {
        IAzureDomainWorkItemClient client = CreateClient();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => client.GetWorkItemsAssignedToAsync(assignee));

        Assert.Contains("empty", exception.Message);
    }

    [Fact]
    public async Task GetNeedsAttentionWorkItemsAsync_requires_team_context()
    {
        IAzureDomainWorkItemClient client = CreateClient();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.GetNeedsAttentionWorkItemsAsync());

        Assert.Contains("FARM_AZURE_DEVOPS_TEAM", exception.Message);
        Assert.Contains("@CurrentIteration", exception.Message);
    }

    [Fact]
    public void BuildNeedsAttentionWiql_escapes_project_and_each_terminal_state()
    {
        var query = AzureDevOpsClient.BuildNeedsAttentionWiql(
            "Owner's Project",
            ["Done", "Ready for O'Brien"]);

        Assert.Contains("[System.TeamProject] = 'Owner''s Project'", query);
        Assert.Contains("[System.State] NOT IN ('Done', 'Ready for O''Brien')", query);
    }

    private static AzureDevOpsClient CreateClient() => new(new AzureDevOpsSettings
    {
        OrganizationUrl = new Uri("https://dev.azure.com/example"),
        Project = "Project",
        PersonalAccessToken = "token"
    });
}