using Xunit;

namespace Farm.Azure.Tests;

public sealed class AzureDevOpsClientTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetWorkItemsAsync_rejects_non_positive_ids(int id)
    {
        using var client = CreateClient();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => client.GetWorkItemsAsync([id]));

        Assert.Contains("positive", exception.Message);
    }

    [Fact]
    public async Task GetWorkItemsAsync_rejects_duplicate_ids()
    {
        using var client = CreateClient();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => client.GetWorkItemsAsync([1, 1]));

        Assert.Contains("duplicates", exception.Message);
    }

    private static AzureDevOpsClient CreateClient() => new(new AzureDevOpsSettings
    {
        OrganizationUrl = new Uri("https://dev.azure.com/example"),
        Project = "Project",
        PersonalAccessToken = "token"
    });
}