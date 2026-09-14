using System;
using System.Threading;
using System.Threading.Tasks;

namespace Shed.Services
{
    public interface ISyncStateStore
    {
        Task<TResult> ReadAsync<TResult>(Func<SyncStateDocument, TResult> action, CancellationToken cancellationToken);
        Task<TResult> MutateAsync<TResult>(Func<SyncStateDocument, TResult> action, CancellationToken cancellationToken);
    }
}


