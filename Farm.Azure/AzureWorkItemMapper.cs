using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;

namespace Farm.Azure;

public static class AzureWorkItemMapper
{
    public static AzureWorkItemDto ToDto(WorkItem workItem)
    {
        ArgumentNullException.ThrowIfNull(workItem);

        return new AzureWorkItemDto(
            workItem.Id ?? 0,
            workItem.Rev ?? 0,
            GetString(workItem, "System.Title"),
            GetString(workItem, "System.State"),
            GetString(workItem, "System.WorkItemType"),
            GetString(workItem, "System.AssignedTo"),
            workItem.Url);
    }

    private static string? GetString(WorkItem workItem, string fieldName) =>
        workItem.Fields.TryGetValue(fieldName, out var value) ? value?.ToString() : null;
}