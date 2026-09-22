using Farm.Core.Domain;
using Farm.Core.Exports;

namespace Farm.Core.Contracts;

public interface IWorkItemSource
{
    Task<IReadOnlyList<WorkItem>> GetWorkItemsAsync(
        IEnumerable<int> ids,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkItem>> GetWorkItemsAssignedToAsync(
        string assignee,
        CancellationToken cancellationToken = default);
}

public interface IPullRequestSource
{
    Task<IReadOnlyList<PullRequestSummary>> ListActiveAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PullRequestSummary>> ListApprovedByCurrentUserAsync(
        CancellationToken cancellationToken = default);
}

public interface IIdentitySource
{
    Task<IReadOnlyList<Identity>> GetIdentitiesAsync(
        IEnumerable<string> ids,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Group>> GetGroupsAsync(
        IEnumerable<string> ids,
        CancellationToken cancellationToken = default);
}

public interface IIdentityDirectory
{
    Task<Identity> GetCurrentUserAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Group>> GetGroupsForCurrentUserAsync(CancellationToken cancellationToken = default);
}

    public interface IWorkspaceExportService
    {
        Task<WorkspaceExportManifest> ExportAsync(
        WorkspaceExportSpecification specification,
        CancellationToken cancellationToken = default);
    }