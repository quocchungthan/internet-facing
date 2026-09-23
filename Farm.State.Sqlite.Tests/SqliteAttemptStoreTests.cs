using Farm.Core.Chickens;
using Xunit;

namespace Farm.State.Sqlite.Tests;

public sealed class SqliteAttemptStoreTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"farm-state-{Guid.NewGuid():N}");

    [Fact]
    public async Task Lease_is_exclusive_and_completion_is_idempotent()
    {
        var store = new SqliteAttemptStore(Path.Combine(root, "state.db"));
        var key = new AttemptKey("org", "project", "repo", 1, "sha", "fingerprint");

        var first = await store.TryAcquireLeaseAsync(key, "owner-1", TimeSpan.FromMinutes(5));
        var second = await store.TryAcquireLeaseAsync(key, "owner-2", TimeSpan.FromMinutes(5));

        Assert.NotNull(first);
        Assert.Null(second);
        await store.CompleteAsync(first!, new AttemptState(key, ReviewOutcomeKind.ChangesProduced, DateTimeOffset.UtcNow, root));
        Assert.True(await store.HasCompletedAttemptAsync(key));
        Assert.Null(await store.TryAcquireLeaseAsync(key, "owner-2", TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public async Task Concurrent_acquisition_has_one_winner()
    {
        var store = new SqliteAttemptStore(Path.Combine(root, "race.db"));
        var key = Key();

        var leases = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(index => store.TryAcquireLeaseAsync(key, $"owner-{index}", TimeSpan.FromMinutes(5))));

        Assert.Single(leases, lease => lease is not null);
    }

    [Fact]
    public async Task Expired_lease_can_be_reacquired_but_stale_owner_cannot_complete()
    {
        var store = new SqliteAttemptStore(Path.Combine(root, "expired.db"));
        var key = Key();
        var stale = await store.TryAcquireLeaseAsync(key, "stale", TimeSpan.FromMilliseconds(20));
        await Task.Delay(50);

        var current = await store.TryAcquireLeaseAsync(key, "current", TimeSpan.FromMinutes(1));

        Assert.NotNull(current);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.CompleteAsync(
            stale!, new AttemptState(key, ReviewOutcomeKind.ChangesProduced, DateTimeOffset.UtcNow, root)));
    }

    [Fact]
    public async Task Live_owner_can_renew_lease()
    {
        var store = new SqliteAttemptStore(Path.Combine(root, "renew.db"));
        var lease = await store.TryAcquireLeaseAsync(Key(), "owner", TimeSpan.FromMinutes(1));

        var renewed = await store.RenewLeaseAsync(lease!, TimeSpan.FromMinutes(5));

        Assert.NotNull(renewed);
        Assert.True(renewed!.ExpiresAt > lease!.ExpiresAt);
    }

    private static AttemptKey Key() => new("org", "project", "repo", 1, "sha", "fingerprint");

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}