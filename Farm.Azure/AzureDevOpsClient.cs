using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;

namespace Farm.Azure;

public sealed class AzureDevOpsClient : IAzureDevOpsClient, IDisposable
{
    private readonly VssConnection connection;
    private readonly string project;
    private WorkItemTrackingHttpClient? client;

    public AzureDevOpsClient(AzureDevOpsSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();
        project = settings.Project;

        var credentials = new VssBasicCredential(string.Empty, settings.PersonalAccessToken);
        connection = new VssConnection(settings.OrganizationUrl, credentials);
    }

    public async Task<IReadOnlyList<AzureWorkItemDto>> GetWorkItemsAsync(
        IEnumerable<int> ids,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        var workItemIds = ids.ToList();
        if (workItemIds.Any(id => id <= 0))
        {
            throw new ArgumentException("Work item IDs must be positive.", nameof(ids));
        }

        if (workItemIds.Count != workItemIds.Distinct().Count())
        {
            throw new ArgumentException("Work item IDs must not contain duplicates.", nameof(ids));
        }

        client ??= connection.GetClient<WorkItemTrackingHttpClient>();
        var workItems = await client.GetWorkItemsAsync(
            project,
            workItemIds,
            cancellationToken: cancellationToken);

        return workItems.Select(AzureWorkItemMapper.ToDto).ToArray();
    }

    public void Dispose() => client?.Dispose();
}