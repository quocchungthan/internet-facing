using Farm.Core.Domain;
using Farm.Core.Exports;

namespace Farm.Core.Contracts;

public interface IWorkItemSource
{
    Task<IReadOnlyList<WorkItem>> GetWorkItemsAsync(
        IEnumerable<int> ids,
        CancellationToken cancellationToken = default);
}

public interface IPullRequestSource
{
    Task<IReadOnlyList<PullRequest>> GetPullRequestsAsync(
        string repositoryId,
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

    public interface IWorkspaceExportService
    {
        Task<WorkspaceExportManifest> ExportAsync(
        WorkspaceExportSpecification specification,
        CancellationToken cancellationToken = default);
    }