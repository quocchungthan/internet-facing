using System.Text.Json;
using Farm.Copilot;
using Farm.Core.Chickens;

namespace Farm.Sandbox.Chickens;

public sealed class ChickenRunner(
    IReviewContextSource contextSource,
    IRepositoryWorkspaceManager workspaces,
    IReviewBrain brain,
    IAttemptStore attempts,
    ChickenOptions options,
    ISensitiveDataRedactor redactor,
    ISensitiveContentScanner contentScanner,
    ILogger<ChickenRunner> logger)
{
    private readonly string ownerId = $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";

    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        var currentUserId = await contextSource.GetCurrentUserIdAsync(cancellationToken);
        var candidates = await contextSource.GetCandidatesAsync(cancellationToken);
        foreach (var candidate in candidates)
        {
            await ProcessCandidateAsync(candidate, currentUserId, cancellationToken);
        }
    }

    private async Task ProcessCandidateAsync(
        ReviewCandidate candidate,
        string currentUserId,
        CancellationToken cancellationToken)
    {
        var eligibility = ReviewEligibilityEvaluator.Evaluate(
            candidate,
            currentUserId,
            DateTimeOffset.UtcNow,
            options.QuietPeriod);
        if (!eligibility.IsEligible)
        {
            logger.LogInformation("PR {PullRequestId} deferred: {Reason}.", candidate.PullRequestId, eligibility.Reason);
            return;
        }

        var key = new AttemptKey(
            candidate.Organization,
            candidate.Project,
            candidate.RepositoryId,
            candidate.PullRequestId,
            candidate.HeadSha,
            eligibility.Fingerprint.Value);
        var lease = await attempts.TryAcquireLeaseAsync(key, ownerId, options.LeaseDuration, cancellationToken);
        if (lease is null)
        {
            return;
        }

        using var execution = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var renewal = RenewLeaseAsync(lease, execution);
        var leaseCompleted = false;
        try
        {
            leaseCompleted = await ExecuteAsync(candidate, eligibility.Fingerprint, lease, execution.Token);
        }
        catch (ReviewDeferredException exception)
        {
            logger.LogWarning("PR {PullRequestId} deferred: {Reason}", candidate.PullRequestId, redactor.Redact(exception.Message));
            await WriteTerminalOutcomeAsync(
                candidate, eligibility.Fingerprint, ReviewOutcomeKind.Deferred, exception, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogError("PR {PullRequestId} failed: {Error}", candidate.PullRequestId, redactor.Redact(exception.ToString()));
            await WriteTerminalOutcomeAsync(
                candidate, eligibility.Fingerprint, ReviewOutcomeKind.Failed, exception, cancellationToken);
        }
        finally
        {
            execution.Cancel();
            try
            {
                await renewal;
            }
            finally
            {
                if (!leaseCompleted)
                {
                    await attempts.ReleaseAsync(lease, CancellationToken.None);
                }
            }
        }
    }

    private async Task<bool> ExecuteAsync(
        ReviewCandidate candidate,
        FeedbackFingerprint fingerprint,
        Lease lease,
        CancellationToken cancellationToken)
    {
        var artifactDirectory = BuildArtifactDirectory(
            options.ArtifactsPath, candidate.RepositoryName, candidate.PullRequestId, fingerprint.Value);
        Directory.CreateDirectory(artifactDirectory);

        var context = await contextSource.GetContextAsync(candidate, cancellationToken);
        await WriteJsonAsync(Path.Combine(artifactDirectory, "context.json"), context, cancellationToken);
        RepositoryWorkspace? workspace = null;
        try
        {
            workspace = await workspaces.PrepareAsync(candidate, fingerprint, cancellationToken);
            var brainOutcome = await brain.ResolveAsync(
                new CopilotReviewRequest(context, workspace, CopilotOverrideLoader.DefaultPurpose, artifactDirectory),
                cancellationToken);

            var readiness = await workspaces.VerifyReadyAsync(workspace, cancellationToken);
            var patch = readiness.Succeeded
                ? await workspaces.CreatePatchAsync(workspace, cancellationToken)
                : string.Empty;
            var validation = readiness.Succeeded
                ? await workspaces.ValidateAsync(workspace, cancellationToken)
                : readiness;
            await WriteTextAsync(Path.Combine(artifactDirectory, "changes.patch"), patch, cancellationToken);
            await WriteTextAsync(Path.Combine(artifactDirectory, "validation.txt"), validation.Output, cancellationToken);

            var outcome = DetermineOutcome(brainOutcome, patch, validation);
            if (outcome.Kind == ReviewOutcomeKind.ChangesProduced)
            {
                var publication = await workspaces.CommitAndPushAsync(
                    workspace, candidate, fingerprint, cancellationToken);
                await WriteJsonAsync(Path.Combine(artifactDirectory, "publication.json"), publication, cancellationToken);
                outcome = publication.Succeeded && publication.Pushed
                    ? outcome with
                    {
                        Summary = $"{outcome.Summary} {publication.Summary}",
                        CommitSha = publication.CommitSha,
                        Published = true
                    }
                    : new ReviewOutcome(
                        ReviewOutcomeKind.Failed,
                        publication.Summary,
                        brainOutcome.Transcript,
                        publication.CommitSha,
                        false);
            }
            if (outcome.Kind == ReviewOutcomeKind.ExplanationOnly)
            {
                await WriteTextAsync(
                    Path.Combine(artifactDirectory, "explanation.md"), outcome.Transcript ?? outcome.Summary, cancellationToken);
            }

            await WriteJsonAsync(Path.Combine(artifactDirectory, "result.json"), outcome, cancellationToken);
            if (outcome.Transcript is not null)
            {
                await WriteTextAsync(Path.Combine(artifactDirectory, "transcript.md"), outcome.Transcript, cancellationToken);
            }

            if (outcome.Kind is ReviewOutcomeKind.ChangesProduced or ReviewOutcomeKind.ExplanationOnly)
            {
                await attempts.CompleteAsync(
                    lease,
                    new AttemptState(lease.Key, outcome.Kind, DateTimeOffset.UtcNow, artifactDirectory),
                    cancellationToken);
                return true;
            }

            return false;
        }
        finally
        {
            if (workspace is not null)
            {
                await workspaces.CleanupAsync(workspace, CancellationToken.None);
            }
        }
    }

    public static ReviewOutcome DetermineOutcome(ReviewOutcome brainOutcome, string patch, ValidationResult validation)
    {
        if (brainOutcome.Kind == ReviewOutcomeKind.Failed)
        {
            return brainOutcome;
        }

        if (!string.IsNullOrWhiteSpace(patch))
        {
            return validation.Succeeded
                ? brainOutcome with { Kind = ReviewOutcomeKind.ChangesProduced }
                : new ReviewOutcome(ReviewOutcomeKind.Failed, "Code changes failed configured validation.", brainOutcome.Transcript);
        }

        return brainOutcome.Kind == ReviewOutcomeKind.ExplanationOnly && !string.IsNullOrWhiteSpace(brainOutcome.Summary)
            ? brainOutcome
            : new ReviewOutcome(ReviewOutcomeKind.Failed, "No code changes or reviewer explanation were produced.", brainOutcome.Transcript);
    }

    private async Task RenewLeaseAsync(Lease initialLease, CancellationTokenSource execution)
    {
        try
        {
            var lease = initialLease;
            var interval = TimeSpan.FromTicks(Math.Max(1, options.LeaseDuration.Ticks / 2));
            while (!execution.IsCancellationRequested)
            {
                await Task.Delay(interval, execution.Token);
                lease = await attempts.RenewLeaseAsync(lease, options.LeaseDuration, execution.Token)
                    ?? throw new InvalidOperationException("Attempt lease ownership was lost during execution.");
            }
        }
        catch (OperationCanceledException) when (execution.IsCancellationRequested)
        {
        }
        catch
        {
            execution.Cancel();
            throw;
        }
    }

    private async Task WriteTerminalOutcomeAsync(
        ReviewCandidate candidate,
        FeedbackFingerprint fingerprint,
        ReviewOutcomeKind kind,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var artifactDirectory = BuildArtifactDirectory(
            options.ArtifactsPath, candidate.RepositoryName, candidate.PullRequestId, fingerprint.Value);
        Directory.CreateDirectory(artifactDirectory);
        var outcome = new ReviewOutcome(kind, exception.Message, exception.ToString());
        await WriteJsonAsync(Path.Combine(artifactDirectory, "result.json"), outcome, cancellationToken);
        await WriteTextAsync(Path.Combine(artifactDirectory, "transcript.md"), exception.ToString(), cancellationToken);
    }

    private Task WriteJsonAsync<T>(string path, T value, CancellationToken cancellationToken) =>
        WriteTextAsync(path, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);

    private Task WriteTextAsync(string path, string value, CancellationToken cancellationToken)
    {
        var redacted = redactor.Redact(value);
        var findings = contentScanner.Find(redacted);
        if (findings.Count > 0)
        {
            File.Delete(path);
            throw new InvalidOperationException(
                $"Refusing to persist sensitive content containing: {string.Join(", ", findings)}.");
        }

        return File.WriteAllTextAsync(path, redacted, cancellationToken);
    }

    internal static string BuildArtifactDirectory(
        string artifactsPath,
        string repositoryName,
        int pullRequestId,
        string fingerprint)
    {
        var root = Path.GetFullPath(artifactsPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var repositorySegment = string.Concat(repositoryName.Select(character =>
            character is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '-'
                ? character
                : '_'));
        if (repositorySegment.Length == 0)
        {
            throw new InvalidOperationException("Repository name cannot produce an empty artifact directory segment.");
        }

        var candidate = Path.GetFullPath(Path.Combine(root, repositorySegment, $"pr-{pullRequestId}", fingerprint));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!candidate.StartsWith(root + Path.DirectorySeparatorChar, comparison))
        {
            throw new InvalidOperationException("Artifact directory must remain beneath the configured artifacts path.");
        }

        return candidate;
    }
}