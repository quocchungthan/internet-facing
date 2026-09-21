using Farm.Azure;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.WebApi;
using Xunit;

namespace Farm.Azure.Tests;

public sealed class AzureWorkItemMapperTests
{
    [Fact]
    public void ToDomain_maps_known_fields_and_preserves_assigned_identity()
    {
        var workItem = new WorkItem
        {
            Id = 42,
            Rev = 3,
            Url = "https://dev.azure.com/example/_apis/wit/workItems/42",
            Fields = new Dictionary<string, object>
            {
                ["System.Title"] = "Ship it",
                ["System.State"] = "New",
                ["System.WorkItemType"] = "Task",
                ["System.AssignedTo"] = new IdentityRef
                {
                    Id = "identity-id",
                    DisplayName = "Person Example",
                    UniqueName = "person@example.com"
                }
            }
        };

        var result = AzureWorkItemMapper.ToDomain(workItem);

        Assert.Equal(42, result.Id);
        Assert.Equal(3, result.Revision);
        Assert.Equal("Ship it", result.Title);
        Assert.Equal("New", result.State);
        Assert.Equal("Task", result.WorkItemType);
        Assert.Equal("identity-id", result.AssignedTo?.Id);
        Assert.Equal("Person Example", result.AssignedTo?.DisplayName);
        Assert.Equal("person@example.com", result.AssignedTo?.UniqueName);
        Assert.Equal(workItem.Url, result.Url?.ToString());
        Assert.Empty(result.Attachments);
    }
}