using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
using CoreIdentity = Farm.Core.Domain.Identity;
using CorePullRequestReviewer = Farm.Core.Domain.PullRequestReviewer;
using CorePullRequestSummary = Farm.Core.Domain.PullRequestSummary;

namespace Farm.Azure;

public static class AzurePullRequestMapper
{
    public static CorePullRequestSummary ToSummary(GitPullRequest pullRequest)
    {
        ArgumentNullException.ThrowIfNull(pullRequest);

        var reviewers = (pullRequest.Reviewers ?? [])
            .Select(ToReviewer)
            .ToArray();

        return new CorePullRequestSummary(
            pullRequest.PullRequestId,
            pullRequest.Title,
            pullRequest.Status.ToString(),
            ToIdentity(pullRequest.CreatedBy),
            new Uri(pullRequest.Url),
            reviewers);
    }

    public static CorePullRequestReviewer ToReviewer(IdentityRefWithVote reviewer)
    {
        ArgumentNullException.ThrowIfNull(reviewer);

        return new CorePullRequestReviewer(
            new CoreIdentity(reviewer.Id, reviewer.DisplayName, reviewer.UniqueName),
            reviewer.Vote,
            reviewer.IsRequired);
    }

    private static CoreIdentity? ToIdentity(IdentityRef? identity) =>
        identity is null ? null : new CoreIdentity(identity.Id, identity.DisplayName, identity.UniqueName);

    // Azure DevOps vote codes: 10=approved, 5=approved-with-suggestions, 0=none, -5=waiting-for-author, -10=rejected.
    public static bool IsApprovedByReviewer(IReadOnlyList<CorePullRequestReviewer> reviewers, string currentUserId)
    {
        ArgumentNullException.ThrowIfNull(reviewers);

        return reviewers.Any(reviewer =>
            reviewer.Identity.Id == currentUserId && reviewer.Vote >= 5);
    }
}
