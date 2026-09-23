using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Farm.Core.Chickens;

public sealed record ReviewCandidate(
    string Organization,
    string Project,
    string RepositoryId,
    string RepositoryName,
    Uri RepositoryUrl,
    int PullRequestId,
    string Title,
    string SourceRef,
    string TargetRef,
    string HeadSha,
    string AuthorId,
    IReadOnlyList<FeedbackThread> Threads,
    IReadOnlyList<int> LinkedWorkItemIds,
    string? TargetSha = null,
    string? SourceRepositoryId = null,
    string? SourceRepositoryName = null,
    Uri? SourceRepositoryUrl = null);

public sealed record FeedbackThread(
    int Id,
    bool IsResolved,
    FeedbackAnchor? Anchor,
    IReadOnlyList<FeedbackComment> Comments);

public sealed record FeedbackAnchor(
    string? FilePath,
    int? StartLine,
    int? EndLine,
    string? CommitId,
    int? LeftStartLine = null,
    int? LeftStartOffset = null,
    int? LeftEndLine = null,
    int? LeftEndOffset = null,
    int? RightStartLine = null,
    int? RightStartOffset = null,
    int? RightEndLine = null,
    int? RightEndOffset = null,
    int? FirstComparingIteration = null,
    int? SecondComparingIteration = null,
    int? ChangeTrackingId = null,
    string? LeftCommitId = null,
    string? RightCommitId = null);

public sealed record FeedbackComment(
    int Id,
    string AuthorId,
    string AuthorDisplayName,
    string Content,
    DateTimeOffset PublishedAt,
    bool IsDeleted = false);

public sealed record WorkItemContext(
    int Id,
    string? Title,
    string? State,
    string? Description,
    IReadOnlyList<WorkItemComment> Comments,
    IReadOnlyList<WorkItemRelation> Relations,
    IReadOnlyList<WorkItemAttachment> Attachments);

public sealed record WorkItemComment(int Id, string? Author, string Content, DateTimeOffset PublishedAt);

public sealed record WorkItemRelation(string RelationType, Uri Url, string? Name);

public sealed record WorkItemAttachment(string Name, Uri DownloadUrl, string? Comment);

public sealed record ReviewContext(ReviewCandidate Candidate, IReadOnlyList<WorkItemContext> WorkItems);

public sealed record FeedbackFingerprint(string Value)
{
    public static FeedbackFingerprint Create(IEnumerable<FeedbackThread> threads, string? excludedAuthorId = null)
    {
        ArgumentNullException.ThrowIfNull(threads);

        var canonical = threads
            .Where(thread => !thread.IsResolved)
            .OrderBy(thread => thread.Id)
            .SelectMany(thread => thread.Comments
                .Where(comment => !comment.IsDeleted &&
                    (excludedAuthorId is null || !string.Equals(comment.AuthorId, excludedAuthorId, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(comment => comment.Id)
                .Select(comment => new
                {
                    ThreadId = thread.Id,
                    thread.Anchor,
                    CommentId = comment.Id,
                    comment.AuthorId,
                    PublishedAt = comment.PublishedAt.ToUniversalTime(),
                    Content = comment.Content.Replace("\r\n", "\n", StringComparison.Ordinal)
                }))
            .ToArray();
        var encoded = JsonSerializer.SerializeToUtf8Bytes(canonical);

        return new FeedbackFingerprint(Convert.ToHexString(SHA256.HashData(encoded)).ToLowerInvariant());
    }
}

public enum ReviewEligibilityReason
{
    Eligible,
    NotAuthoredByCurrentUser,
    NoUnresolvedExternalFeedback,
    FeedbackStillCoolingDown
}

public sealed record ReviewEligibility(bool IsEligible, ReviewEligibilityReason Reason, FeedbackFingerprint Fingerprint);

public static class ReviewEligibilityEvaluator
{
    public static ReviewEligibility Evaluate(
        ReviewCandidate candidate,
        string currentUserId,
        DateTimeOffset now,
        TimeSpan schedulePeriod)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentUserId);
        if (schedulePeriod < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(schedulePeriod));
        }

        var unresolved = candidate.Threads.Where(thread => !thread.IsResolved).ToArray();
        var fingerprint = FeedbackFingerprint.Create(unresolved, currentUserId);
        if (!string.Equals(candidate.AuthorId, currentUserId, StringComparison.OrdinalIgnoreCase))
        {
            return new(false, ReviewEligibilityReason.NotAuthoredByCurrentUser, fingerprint);
        }

        var externalComments = unresolved
            .SelectMany(thread => thread.Comments)
            .Where(comment => !comment.IsDeleted &&
                !string.Equals(comment.AuthorId, currentUserId, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (externalComments.Length == 0)
        {
            return new(false, ReviewEligibilityReason.NoUnresolvedExternalFeedback, fingerprint);
        }

        if (externalComments.Max(comment => comment.PublishedAt) > now - schedulePeriod)
        {
            return new(false, ReviewEligibilityReason.FeedbackStillCoolingDown, fingerprint);
        }

        return new(true, ReviewEligibilityReason.Eligible, fingerprint);
    }
}

public enum ReviewOutcomeKind
{
    ChangesProduced,
    ExplanationOnly,
    Deferred,
    Failed
}

public sealed record ReviewOutcome(
    ReviewOutcomeKind Kind,
    string Summary,
    string? Transcript = null,
    string? CommitSha = null,
    bool Published = false);

public sealed record AttemptKey(
    string Organization,
    string Project,
    string RepositoryId,
    int PullRequestId,
    string HeadSha,
    string FeedbackFingerprint);

public sealed record AttemptState(AttemptKey Key, ReviewOutcomeKind Outcome, DateTimeOffset CompletedAt, string ArtifactDirectory);

public sealed record Lease(string OwnerId, AttemptKey Key, DateTimeOffset ExpiresAt);

public sealed record RepositoryWorkspace(
    string RepositoryPath,
    string WorktreePath,
    string BranchName,
    string SourceRef,
    string ExpectedOldHeadSha,
    string BaseSha,
    bool HasRebaseConflicts = false);

public sealed record ValidationResult(bool Succeeded, int ExitCode, string Output);

public sealed record PublicationResult(bool Succeeded, bool Pushed, string? CommitSha, string Summary);

public sealed class ReviewDeferredException(string message, Exception? innerException = null) : Exception(message, innerException);

public sealed record CopilotReviewRequest(ReviewContext Context, RepositoryWorkspace Workspace, string Purpose, string ArtifactDirectory);

public interface IReviewContextSource
{
    Task<string> GetCurrentUserIdAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReviewCandidate>> GetCandidatesAsync(CancellationToken cancellationToken = default);
    Task<ReviewContext> GetContextAsync(ReviewCandidate candidate, CancellationToken cancellationToken = default);
}

public interface IAttemptStore
{
    Task<bool> HasCompletedAttemptAsync(AttemptKey key, CancellationToken cancellationToken = default);
    Task<Lease?> TryAcquireLeaseAsync(AttemptKey key, string ownerId, TimeSpan duration, CancellationToken cancellationToken = default);
    Task<Lease?> RenewLeaseAsync(Lease lease, TimeSpan duration, CancellationToken cancellationToken = default);
    Task CompleteAsync(Lease lease, AttemptState attempt, CancellationToken cancellationToken = default);
    Task ReleaseAsync(Lease lease, CancellationToken cancellationToken = default);
}

public interface IRepositoryWorkspaceManager
{
    Task<RepositoryWorkspace> PrepareAsync(
        ReviewCandidate candidate,
        FeedbackFingerprint fingerprint,
        CancellationToken cancellationToken = default);
    Task<ValidationResult> VerifyReadyAsync(RepositoryWorkspace workspace, CancellationToken cancellationToken = default);
    Task<string> CreatePatchAsync(RepositoryWorkspace workspace, CancellationToken cancellationToken = default);
    Task<ValidationResult> ValidateAsync(RepositoryWorkspace workspace, CancellationToken cancellationToken = default);
    Task<PublicationResult> CommitAndPushAsync(
        RepositoryWorkspace workspace,
        ReviewCandidate candidate,
        FeedbackFingerprint fingerprint,
        CancellationToken cancellationToken = default);
    Task CleanupAsync(RepositoryWorkspace workspace, CancellationToken cancellationToken = default);
}

public interface IReviewBrain
{
    Task<ReviewOutcome> ResolveAsync(CopilotReviewRequest request, CancellationToken cancellationToken = default);
}