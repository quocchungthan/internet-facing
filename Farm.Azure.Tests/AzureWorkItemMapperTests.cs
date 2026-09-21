using Farm.Azure;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Xunit;

namespace Farm.Azure.Tests;

public sealed class AzureWorkItemMapperTests
{
    [Fact]
    public void ToDto_maps_known_fields_and_keeps_missing_fields_null()
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
                ["System.AssignedTo"] = "person@example.com"
            }
        };

        var result = AzureWorkItemMapper.ToDto(workItem);

        Assert.Equal(42, result.Id);
        Assert.Equal(3, result.Revision);
        Assert.Equal("Ship it", result.Title);
        Assert.Equal("New", result.State);
        Assert.Equal("Task", result.WorkItemType);
        Assert.Equal("person@example.com", result.AssignedTo);
        Assert.Equal(workItem.Url, result.Url);
    }
}