using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.Identity;
using Microsoft.VisualStudio.Services.Identity.Client;
using Microsoft.VisualStudio.Services.WebApi;
using CoreGroup = Farm.Core.Domain.Group;
using CoreIdentity = Farm.Core.Domain.Identity;
using CorePullRequestSummary = Farm.Core.Domain.PullRequestSummary;
using CoreWorkItem = Farm.Core.Domain.WorkItem;

namespace Farm.Azure;

public sealed class AzureDevOpsClient :
    IAzureDevOpsClient,
    IAzureDomainWorkItemClient,
    IAzureIdentityClient,
    IAzurePullRequestClient,
    IDisposable
{
    private readonly VssConnection connection;
    private readonly string project;
    private WorkItemTrackingHttpClient? client;
    private IdentityHttpClient? identityClient;
    private GitHttpClient? gitClient;
    private CoreIdentity? cachedCurrentUser;

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
        var workItems = await GetAzureWorkItemsAsync(ids, cancellationToken);
        return workItems.Select(AzureWorkItemMapper.ToDto).ToArray();
    }

    public async Task<IReadOnlyList<CoreWorkItem>> GetDomainWorkItemsAsync(
        IEnumerable<int> ids,
        CancellationToken cancellationToken = default)
    {
        var workItems = await GetAzureWorkItemsAsync(ids, cancellationToken);
        return workItems.Select(AzureWorkItemMapper.ToDomain).ToArray();
    }

    public async Task<IReadOnlyList<CoreWorkItem>> GetWorkItemsAssignedToAsync(
        string assignee,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(assignee))
        {
            throw new ArgumentException("Assignee must not be empty.", nameof(assignee));
        }

        client ??= connection.GetClient<WorkItemTrackingHttpClient>();

        var assignedToClause = string.Equals(assignee, "me", StringComparison.OrdinalIgnoreCase)
            ? "@Me"
            : $"'{EscapeWiqlLiteral(assignee)}'";

        var wiql = new Wiql
        {
            Query = $"SELECT [System.Id] FROM WorkItems " +
                    $"WHERE [System.TeamProject] = '{EscapeWiqlLiteral(project)}' " +
                    $"AND [System.AssignedTo] = {assignedToClause} " +
                    "ORDER BY [System.ChangedDate] DESC"
        };

        var queryResult = await client.QueryByWiqlAsync(wiql, cancellationToken: cancellationToken);
        var ids = queryResult.WorkItems.Select(reference => reference.Id).ToArray();
        if (ids.Length == 0)
        {
            return [];
        }

        return await GetDomainWorkItemsAsync(ids, cancellationToken);
    }

    public async Task<CoreIdentity> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        if (cachedCurrentUser is not null)
        {
            return cachedCurrentUser;
        }

        identityClient ??= connection.GetClient<IdentityHttpClient>();
        var self = await identityClient.GetIdentitySelfAsync(cancellationToken: cancellationToken);
        cachedCurrentUser = AzureIdentityMapper.ToDomain(self);
        return cachedCurrentUser;
    }

    public async Task<IReadOnlyList<CoreGroup>> GetGroupsForCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        identityClient ??= connection.GetClient<IdentityHttpClient>();
        var self = await identityClient.GetIdentitySelfAsync(cancellationToken: cancellationToken);

        var expanded = await identityClient.ReadIdentitiesAsync(
            new List<Guid> { self.Id },
            QueryMembership.Direct,
            propertyNameFilters: null,
            includeRestrictedVisibility: false,
            userState: null,
            cancellationToken: cancellationToken);

        var memberOfDescriptors = expanded.FirstOrDefault()?.MemberOf?.ToList() ?? [];
        if (memberOfDescriptors.Count == 0)
        {
            return [];
        }

        var groups = await identityClient.ReadIdentitiesAsync(
            memberOfDescriptors,
            QueryMembership.None,
            propertyNameFilters: null,
            includeRestrictedVisibility: false,
            userState: null,
            cancellationToken: cancellationToken);

        return groups.Select(AzureIdentityMapper.ToGroup).ToArray();
    }

    public async Task<IReadOnlyList<CorePullRequestSummary>> ListActivePullRequestsAsync(
        CancellationToken cancellationToken = default)
    {
        gitClient ??= connection.GetClient<GitHttpClient>();
        var criteria = new GitPullRequestSearchCriteria { Status = PullRequestStatus.Active };
        var pullRequests = await gitClient.GetPullRequestsByProjectAsync(
            project,
            criteria,
            cancellationToken: cancellationToken);

        return pullRequests.Select(AzurePullRequestMapper.ToSummary).ToArray();
    }

    public async Task<IReadOnlyList<CorePullRequestSummary>> ListPullRequestsApprovedByCurrentUserAsync(
        CancellationToken cancellationToken = default)
    {
        var currentUser = await GetCurrentUserAsync(cancellationToken);

        gitClient ??= connection.GetClient<GitHttpClient>();
        var criteria = new GitPullRequestSearchCriteria
        {
            Status = PullRequestStatus.Active,
            ReviewerId = Guid.Parse(currentUser.Id)
        };
        var pullRequests = await gitClient.GetPullRequestsByProjectAsync(
            project,
            criteria,
            cancellationToken: cancellationToken);

        return pullRequests
            .Select(AzurePullRequestMapper.ToSummary)
            .Where(pullRequest => AzurePullRequestMapper.IsApprovedByReviewer(pullRequest.Reviewers, currentUser.Id))
            .ToArray();
    }

    private async Task<IReadOnlyList<WorkItem>> GetAzureWorkItemsAsync(
        IEnumerable<int> ids,
        CancellationToken cancellationToken)
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

        return workItems;
    }

    private static string EscapeWiqlLiteral(string value) => value.Replace("'", "''");

    public void Dispose()
    {
        client?.Dispose();
        identityClient?.Dispose();
        gitClient?.Dispose();
    }
}