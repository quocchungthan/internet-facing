using Farm.Core.Contracts;
using CorePullRequestSummary = Farm.Core.Domain.PullRequestSummary;

namespace Farm.Azure;

public interface IAzurePullRequestClient
{
    Task<IReadOnlyList<CorePullRequestSummary>> ListActivePullRequestsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CorePullRequestSummary>> ListPullRequestsApprovedByCurrentUserAsync(
        CancellationToken cancellationToken = default);
}

public sealed class AzurePullRequestSource(IAzurePullRequestClient client) : IPullRequestSource
{
    public Task<IReadOnlyList<CorePullRequestSummary>> ListActiveAsync(
        CancellationToken cancellationToken = default) =>
        client.ListActivePullRequestsAsync(cancellationToken);

    public Task<IReadOnlyList<CorePullRequestSummary>> ListApprovedByCurrentUserAsync(
        CancellationToken cancellationToken = default) =>
        client.ListPullRequestsApprovedByCurrentUserAsync(cancellationToken);
}
