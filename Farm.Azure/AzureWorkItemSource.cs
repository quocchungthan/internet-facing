using Farm.Core.Contracts;
using CoreWorkItem = Farm.Core.Domain.WorkItem;

namespace Farm.Azure;

public interface IAzureDomainWorkItemClient
{
    Task<IReadOnlyList<CoreWorkItem>> GetDomainWorkItemsAsync(
        IEnumerable<int> ids,
        CancellationToken cancellationToken = default);
}

public sealed class AzureWorkItemSource(IAzureDomainWorkItemClient client) : IWorkItemSource
{
    public Task<IReadOnlyList<CoreWorkItem>> GetWorkItemsAsync(
        IEnumerable<int> ids,
        CancellationToken cancellationToken = default) =>
        client.GetDomainWorkItemsAsync(ids, cancellationToken);
}