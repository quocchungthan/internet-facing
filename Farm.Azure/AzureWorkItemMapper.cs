using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.WebApi;
using AzureWorkItem = Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem;
using CoreIdentity = Farm.Core.Domain.Identity;
using CoreWorkItem = Farm.Core.Domain.WorkItem;

namespace Farm.Azure;

public static class AzureWorkItemMapper
{
    public static AzureWorkItemDto ToDto(AzureWorkItem workItem)
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

    public static CoreWorkItem ToDomain(AzureWorkItem workItem)
    {
        ArgumentNullException.ThrowIfNull(workItem);

        return new CoreWorkItem(
            workItem.Id ?? 0,
            workItem.Rev ?? 0,
            GetString(workItem, "System.Title"),
            GetString(workItem, "System.State"),
            GetString(workItem, "System.WorkItemType"),
                GetIdentity(workItem, "System.AssignedTo"),
            CreateUri(workItem.Url),
            []);
    }

    private static string? GetString(AzureWorkItem workItem, string fieldName) =>
        workItem.Fields.TryGetValue(fieldName, out var value) ? value?.ToString() : null;

    private static CoreIdentity? GetIdentity(AzureWorkItem workItem, string fieldName) =>
        workItem.Fields.TryGetValue(fieldName, out var value) && value is IdentityRef identity
            ? new CoreIdentity(identity.Id, identity.DisplayName, identity.UniqueName)
            : null;

    private static Uri? CreateUri(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) ? uri : null;
}