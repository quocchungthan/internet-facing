using Farm.Core.Chickens;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Farm.Sandbox.Chickens.Tests;

public sealed class ChickenWorkerTests
{
    private static readonly string[] Secrets =
    [
        "azure-pat-sentinel",
        "copilot-token-sentinel",
        "git-secret-sentinel"
    ];

    [Fact]
    public async Task Cycle_failure_logs_only_redacted_exception_text()
    {
        var redactor = new SensitiveDataRedactor(Secrets);
        var logger = new CapturingLogger<ChickenWorker>();
        var options = new ChickenOptions
        {
            ArtifactsPath = Path.GetTempPath(),
            SchedulePeriod = TimeSpan.FromHours(1),
            RunImmediately = true
        };
        var runner = new ChickenRunner(
            new ThrowingContextSource(string.Join(" ", Secrets)),
            new UnusedWorkspaceManager(),
            new UnusedBrain(),
            new UnusedAttemptStore(),
            options,
            redactor,
            SensitiveContentScanner.Empty,
            new CapturingLogger<ChickenRunner>());
        var worker = new ChickenWorker(runner, options, redactor, logger);

        await worker.StartAsync(CancellationToken.None);
        await logger.WaitForLogAsync(TimeSpan.FromSeconds(5));
        await worker.StopAsync(CancellationToken.None);

        var captured = string.Join(Environment.NewLine, logger.Entries.SelectMany(entry =>
            new[] { entry.Message, entry.Exception?.ToString() ?? string.Empty }));
        Assert.Contains("[REDACTED]", captured);
        Assert.All(Secrets, secret => Assert.DoesNotContain(secret, captured, StringComparison.Ordinal));
        Assert.All(logger.Entries, entry => Assert.Null(entry.Exception));
    }

    private sealed class ThrowingContextSource(string message) : IReviewContextSource
    {
        public Task<string> GetCurrentUserIdAsync(CancellationToken cancellationToken = default) =>
            Task.FromException<string>(new InvalidOperationException(message));

        public Task<IReadOnlyList<ReviewCandidate>> GetCandidatesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ReviewContext> GetContextAsync(ReviewCandidate candidate, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class UnusedWorkspaceManager : IRepositoryWorkspaceManager
    {
        public Task<RepositoryWorkspace> PrepareAsync(ReviewCandidate candidate, FeedbackFingerprint fingerprint, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ValidationResult> VerifyReadyAsync(RepositoryWorkspace workspace, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string> CreatePatchAsync(RepositoryWorkspace workspace, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ValidationResult> ValidateAsync(RepositoryWorkspace workspace, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PublicationResult> CommitAndPushAsync(RepositoryWorkspace workspace, ReviewCandidate candidate, FeedbackFingerprint fingerprint, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task CleanupAsync(RepositoryWorkspace workspace, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class UnusedBrain : IReviewBrain
    {
        public Task<ReviewOutcome> ResolveAsync(CopilotReviewRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class UnusedAttemptStore : IAttemptStore
    {
        public Task<bool> HasCompletedAttemptAsync(AttemptKey key, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Lease?> TryAcquireLeaseAsync(AttemptKey key, string ownerId, TimeSpan duration, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Lease?> RenewLeaseAsync(Lease lease, TimeSpan duration, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task CompleteAsync(Lease lease, AttemptState attempt, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task ReleaseAsync(Lease lease, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        private readonly TaskCompletionSource firstLog = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new LogEntry(formatter(state, exception), exception));
            firstLog.TrySetResult();
        }

        public Task WaitForLogAsync(TimeSpan timeout) => firstLog.Task.WaitAsync(timeout);
    }

    private sealed record LogEntry(string Message, Exception? Exception);
}