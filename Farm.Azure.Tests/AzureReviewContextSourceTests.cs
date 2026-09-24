using Farm.Core.Chickens;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
using Xunit;

namespace Farm.Azure.Tests;

public sealed class AzureReviewContextSourceTests
{
    [Fact]
    public void MapThread_carries_both_file_sides_and_iteration_context_into_core()
    {
        var thread = new GitPullRequestCommentThread
        {
            Id = 7,
            Status = CommentThreadStatus.Active,
            ThreadContext = new CommentThreadContext
            {
                FilePath = "/file.cs",
                LeftFileStart = new CommentPosition { Line = 10, Offset = 2 },
                LeftFileEnd = new CommentPosition { Line = 11, Offset = 3 },
                RightFileStart = new CommentPosition { Line = 20, Offset = 4 },
                RightFileEnd = new CommentPosition { Line = 21, Offset = 5 }
            },
            PullRequestThreadContext = new GitPullRequestCommentThreadContext
            {
                ChangeTrackingId = 42,
                IterationContext = new CommentIterationContext
                {
                    FirstComparingIteration = 3,
                    SecondComparingIteration = 4
                }
            },
            Comments = []
        };

        FeedbackThread mapped = AzureDevOpsClient.MapThread(thread, "left-sha", "right-sha");

        Assert.Equal(10, mapped.Anchor!.LeftStartLine);
        Assert.Equal(21, mapped.Anchor.RightEndLine);
        Assert.Equal(3, mapped.Anchor.FirstComparingIteration);
        Assert.Equal(4, mapped.Anchor.SecondComparingIteration);
        Assert.Equal(42, mapped.Anchor.ChangeTrackingId);
        Assert.Equal("left-sha", mapped.Anchor.LeftCommitId);
        Assert.Equal("right-sha", mapped.Anchor.RightCommitId);
        Assert.Equal(typeof(FeedbackAnchor).Assembly, mapped.Anchor.GetType().Assembly);
    }

    [Fact]
    public void MapCandidate_carries_full_target_sha_into_core()
    {
        var targetSha = new string('b', 40);

        var candidate = AzureDevOpsClient.MapCandidate(
            PullRequest(new string('a', 40), targetSha), [], [], new Uri("https://dev.azure.com/org"), "project");

        Assert.Equal(targetSha, candidate.TargetSha);
    }

    [Fact]
    public void MapCandidate_uses_target_repository_as_source_for_non_fork()
    {
        var pullRequest = PullRequest(new string('a', 40), new string('b', 40));

        var candidate = AzureDevOpsClient.MapCandidate(
            pullRequest, [], [], new Uri("https://dev.azure.com/org"), "project");

        Assert.Equal(candidate.RepositoryId, candidate.SourceRepositoryId);
        Assert.Equal(candidate.RepositoryName, candidate.SourceRepositoryName);
        Assert.Equal(candidate.RepositoryUrl, candidate.SourceRepositoryUrl);
    }

    [Fact]
    public void MapCandidate_uses_fork_repository_as_source()
    {
        var pullRequest = PullRequest(new string('a', 40), new string('b', 40));
        var forkRepository = new GitRepository
        {
            Id = Guid.NewGuid(),
            Name = "fork-repo",
            RemoteUrl = "https://fork.example.test/repo"
        };
        pullRequest.ForkSource = new GitForkRef { Repository = forkRepository };

        var candidate = AzureDevOpsClient.MapCandidate(
            pullRequest, [], [], new Uri("https://dev.azure.com/org"), "project");

        Assert.Equal(pullRequest.Repository.Id.ToString(), candidate.RepositoryId);
        Assert.Equal(new Uri("https://example.test/repo"), candidate.RepositoryUrl);
        Assert.Equal(forkRepository.Id.ToString(), candidate.SourceRepositoryId);
        Assert.Equal("fork-repo", candidate.SourceRepositoryName);
        Assert.Equal(new Uri("https://fork.example.test/repo"), candidate.SourceRepositoryUrl);
    }

    [Fact]
    public void MapCandidate_derives_escaped_target_repository_url_when_absolute_urls_are_missing()
    {
        var pullRequest = PullRequest(new string('a', 40), new string('b', 40));
        pullRequest.Repository.RemoteUrl = null;
        pullRequest.Repository.WebUrl = null;
        pullRequest.Repository.Name = "repo/name";

        var candidate = AzureDevOpsClient.MapCandidate(
            pullRequest, [], [], new Uri("https://dev.azure.com/example"), "Project / One");

        Assert.Equal(
            new Uri("https://dev.azure.com/example/Project%20%2F%20One/_git/repo%2Fname"),
            candidate.RepositoryUrl);
    }

    [Fact]
    public void MapCandidate_derives_fork_source_url_separately_from_target_url()
    {
        var pullRequest = PullRequest(new string('a', 40), new string('b', 40));
        pullRequest.Repository.RemoteUrl = null;
        pullRequest.Repository.WebUrl = null;
        pullRequest.ForkSource = new GitForkRef
        {
            Repository = new GitRepository
            {
                Id = Guid.NewGuid(),
                Name = "fork/repo"
            }
        };

        var candidate = AzureDevOpsClient.MapCandidate(
            pullRequest, [], [], new Uri("https://dev.azure.com/example"), "Project");

        Assert.Equal(new Uri("https://dev.azure.com/example/Project/_git/repo"), candidate.RepositoryUrl);
        Assert.Equal(new Uri("https://dev.azure.com/example/Project/_git/fork%2Frepo"), candidate.SourceRepositoryUrl);
    }

    [Fact]
    public void TryMapCandidate_skips_malformed_candidate_without_blocking_valid_candidate()
    {
        var malformed = PullRequest(new string('a', 40), null);
        var valid = PullRequest(new string('a', 40), new string('b', 40));

        Assert.False(AzureDevOpsClient.TryMapCandidate(
            malformed, [], [], new Uri("https://dev.azure.com/example"), "Project", out _));
        Assert.True(AzureDevOpsClient.TryMapCandidate(
            valid, [], [], new Uri("https://dev.azure.com/example"), "Project", out var candidate));
        Assert.NotNull(candidate);
    }

    [Fact]
    public void TryMapCandidate_returns_stable_skip_reason_for_missing_repository_metadata()
    {
        var pullRequest = PullRequest(new string('a', 40), new string('b', 40));
        pullRequest.Repository.Name = null;
        pullRequest.Repository.Id = Guid.Empty;
        pullRequest.Repository.RemoteUrl = null;
        pullRequest.Repository.WebUrl = null;

        var mapped = AzureDevOpsClient.TryMapCandidate(
            pullRequest, [], [], new Uri("https://dev.azure.com/example"), "Project", out var candidate, out var reason);

        Assert.False(mapped);
        Assert.Null(candidate);
        Assert.Equal("repository_metadata_missing", reason);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("abc")]
    [InlineData("000000000000000000000000000000000000000g")]
    public void MapCandidate_rejects_invalid_target_sha(string? targetSha)
    {
        Assert.Throws<InvalidOperationException>(() => AzureDevOpsClient.MapCandidate(
            PullRequest(new string('a', 40), targetSha), [], [], new Uri("https://dev.azure.com/org"), "project"));
    }

    private static GitPullRequest PullRequest(string headSha, string? targetSha) => new()
    {
        PullRequestId = 42,
        Title = "PR",
        SourceRefName = "refs/heads/feature",
        TargetRefName = "refs/heads/main",
        Repository = new GitRepository
        {
            Id = Guid.NewGuid(),
            Name = "repo",
            RemoteUrl = "https://example.test/repo"
        },
        CreatedBy = new IdentityRef { Id = "author" },
        LastMergeSourceCommit = new GitCommitRef { CommitId = headSha },
        LastMergeTargetCommit = targetSha is null ? null : new GitCommitRef { CommitId = targetSha }
    };
}