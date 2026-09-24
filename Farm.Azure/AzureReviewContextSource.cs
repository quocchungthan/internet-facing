using Farm.Core.Chickens;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using System.Text.RegularExpressions;
using AzureWorkItemComment = Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.Comment;
using AzureWorkItemRelation = Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItemRelation;

namespace Farm.Azure;

public sealed partial class AzureDevOpsClient : IReviewContextSource
{
    private readonly Uri organizationUrl;

    public async Task<string> GetCurrentUserIdAsync(CancellationToken cancellationToken = default) =>
        (await GetCurrentUserAsync(cancellationToken)).Id;

    public async Task<ReviewCandidateDiscoveryResult> GetCandidatesAsync(CancellationToken cancellationToken = default)
    {
        var currentUser = await GetCurrentUserAsync(cancellationToken);
        gitClient ??= connection.GetClient<GitHttpClient>();
        var pullRequests = await gitClient.GetPullRequestsByProjectAsync(
            project,
            new GitPullRequestSearchCriteria
            {
                Status = PullRequestStatus.Active,
                CreatorId = Guid.Parse(currentUser.Id)
            },
            cancellationToken: cancellationToken);

        var candidates = new List<ReviewCandidate>(pullRequests.Count);
        var skippedCandidates = new List<ReviewCandidateSkip>();
        foreach (var pullRequest in pullRequests)
        {
            if (pullRequest.Repository is null)
            {
                skippedCandidates.Add(new(pullRequest.PullRequestId, "repository_metadata_missing"));
                continue;
            }

            try
            {
                var threads = await gitClient.GetThreadsAsync(
                    project,
                    pullRequest.Repository.Id.ToString(),
                    pullRequest.PullRequestId,
                    cancellationToken: cancellationToken);
                var workItemRefs = await gitClient.GetPullRequestWorkItemRefsAsync(
                    project,
                    pullRequest.Repository.Id,
                    pullRequest.PullRequestId,
                    cancellationToken: cancellationToken);
                if (TryMapCandidate(pullRequest, threads, workItemRefs, organizationUrl, project, out var candidate, out var reason) &&
                    candidate is not null)
                {
                    candidates.Add(candidate);
                }
                else
                {
                    skippedCandidates.Add(new(pullRequest.PullRequestId, reason));
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                skippedCandidates.Add(new(pullRequest.PullRequestId, "candidate_context_failed"));
            }
        }

        return new ReviewCandidateDiscoveryResult(candidates, skippedCandidates);
    }

    public async Task<ReviewContext> GetContextAsync(
        ReviewCandidate candidate,
        CancellationToken cancellationToken = default)
    {
        client ??= connection.GetClient<Microsoft.TeamFoundation.WorkItemTracking.WebApi.WorkItemTrackingHttpClient>();
        var workItems = new List<WorkItemContext>(candidate.LinkedWorkItemIds.Count);
        foreach (var id in candidate.LinkedWorkItemIds)
        {
            var workItem = await client.GetWorkItemAsync(
                project,
                id,
                expand: WorkItemExpand.Relations,
                cancellationToken: cancellationToken);
            var comments = await client.GetCommentsAsync(project, id, cancellationToken: cancellationToken);
            workItems.Add(MapWorkItem(workItem, comments.Comments));
        }

        return new ReviewContext(candidate, workItems);
    }

    internal static ReviewCandidate MapCandidate(
        GitPullRequest pullRequest,
        IReadOnlyList<GitPullRequestCommentThread> threads,
        IReadOnlyList<Microsoft.VisualStudio.Services.WebApi.ResourceRef> workItemRefs,
        Uri organizationUrl,
        string project)
    {
        ArgumentNullException.ThrowIfNull(pullRequest);
        ArgumentNullException.ThrowIfNull(pullRequest.Repository);

        var repositoryUri = ResolveRepositoryUrl(
            pullRequest.Repository,
            organizationUrl,
            project,
            pullRequest.PullRequestId,
            "target");

        var sourceRepository = pullRequest.ForkSource?.Repository ?? pullRequest.Repository;
        var sourceRepositoryUri = TryResolveRepositoryUrl(sourceRepository, organizationUrl, project);

        if (pullRequest.ForkSource is not null && sourceRepositoryUri is null)
        {
            throw new InvalidOperationException($"Pull request {pullRequest.PullRequestId} has insufficient source repository metadata.");
        }

        var headSha = RequireFullCommitSha(
            pullRequest.LastMergeSourceCommit?.CommitId, pullRequest.PullRequestId, "source head");
        var targetSha = RequireFullCommitSha(
            pullRequest.LastMergeTargetCommit?.CommitId, pullRequest.PullRequestId, "target");

        return new ReviewCandidate(
            organizationUrl.AbsoluteUri.TrimEnd('/'),
            project,
            pullRequest.Repository.Id.ToString(),
            pullRequest.Repository.Name,
            repositoryUri,
            pullRequest.PullRequestId,
            pullRequest.Title,
            pullRequest.SourceRefName,
            pullRequest.TargetRefName,
            headSha,
            pullRequest.CreatedBy.Id,
            threads.Where(thread => thread.Comments is { Count: > 0 })
                .Select(thread => MapThread(thread, targetSha, headSha)).ToArray(),
            workItemRefs.Select(reference => int.Parse(reference.Id)).ToArray(),
            targetSha,
            sourceRepository.Id.ToString(),
            sourceRepository.Name,
            sourceRepositoryUri);
    }

    internal static bool TryMapCandidate(
        GitPullRequest pullRequest,
        IReadOnlyList<GitPullRequestCommentThread> threads,
        IReadOnlyList<Microsoft.VisualStudio.Services.WebApi.ResourceRef> workItemRefs,
        Uri organizationUrl,
        string project,
        out ReviewCandidate? candidate) =>
        TryMapCandidate(pullRequest, threads, workItemRefs, organizationUrl, project, out candidate, out _);

    internal static bool TryMapCandidate(
        GitPullRequest pullRequest,
        IReadOnlyList<GitPullRequestCommentThread> threads,
        IReadOnlyList<Microsoft.VisualStudio.Services.WebApi.ResourceRef> workItemRefs,
        Uri organizationUrl,
        string project,
        out ReviewCandidate? candidate,
        out string reason)
    {
        if (pullRequest.Repository is null ||
            TryResolveRepositoryUrl(pullRequest.Repository, organizationUrl, project) is null)
        {
            candidate = null;
            reason = "repository_metadata_missing";
            return false;
        }

        if (pullRequest.ForkSource is not null &&
            (pullRequest.ForkSource.Repository is null ||
             TryResolveRepositoryUrl(pullRequest.ForkSource.Repository, organizationUrl, project) is null))
        {
            candidate = null;
            reason = "repository_metadata_missing";
            return false;
        }

        try
        {
            candidate = MapCandidate(pullRequest, threads, workItemRefs, organizationUrl, project);
            reason = string.Empty;
            return true;
        }
        catch (Exception)
        {
            candidate = null;
            reason = "candidate_mapping_failed";
            return false;
        }
    }

    private static Uri ResolveRepositoryUrl(
        GitRepository repository,
        Uri organizationUrl,
        string project,
        int pullRequestId,
        string repositoryRole)
    {
        return TryResolveRepositoryUrl(repository, organizationUrl, project)
            ?? throw new InvalidOperationException(
                $"Pull request {pullRequestId} has insufficient {repositoryRole} repository metadata.");
    }

    private static Uri? TryResolveRepositoryUrl(
        GitRepository? repository,
        Uri organizationUrl,
        string project)
    {
        if (repository is null)
        {
            return null;
        }

        foreach (var value in new[] { repository.RemoteUrl, repository.WebUrl })
        {
            if (Uri.TryCreate(value, UriKind.Absolute, out var absoluteUri))
            {
                return absoluteUri;
            }
        }

        var repositoryPath = !string.IsNullOrWhiteSpace(repository.Name)
            ? repository.Name
            : repository.Id == Guid.Empty ? null : repository.Id.ToString();
        if (string.IsNullOrWhiteSpace(repositoryPath))
        {
            return null;
        }

        var basePath = organizationUrl.AbsoluteUri.TrimEnd('/');
        return Uri.TryCreate(
            $"{basePath}/{Uri.EscapeDataString(project)}/_git/{Uri.EscapeDataString(repositoryPath)}",
            UriKind.Absolute,
            out var derivedUri)
            ? derivedUri
            : null;
    }

    private static string RequireFullCommitSha(string? value, int pullRequestId, string name)
    {
        if (value is null || !FullCommitSha().IsMatch(value))
        {
            throw new InvalidOperationException(
                $"Pull request {pullRequestId} {name} SHA must be a full 40-character hexadecimal commit ID.");
        }

        return value;
    }


    public static FeedbackThread MapThread(GitPullRequestCommentThread thread, string? leftCommitId, string? rightCommitId)
    {
        var context = thread.ThreadContext;
        var pullRequestContext = thread.PullRequestThreadContext;
        var anchor = context is null
            ? null
            : new FeedbackAnchor(
                context.FilePath,
                context.RightFileStart?.Line,
                context.RightFileEnd?.Line,
                rightCommitId,
                context.LeftFileStart?.Line,
                context.LeftFileStart?.Offset,
                context.LeftFileEnd?.Line,
                context.LeftFileEnd?.Offset,
                context.RightFileStart?.Line,
                context.RightFileStart?.Offset,
                context.RightFileEnd?.Line,
                context.RightFileEnd?.Offset,
                pullRequestContext?.IterationContext?.FirstComparingIteration,
                pullRequestContext?.IterationContext?.SecondComparingIteration,
                pullRequestContext?.ChangeTrackingId,
                leftCommitId,
                rightCommitId);
        var comments = (thread.Comments ?? [])
            .Select(comment => new FeedbackComment(
                comment.Id,
                comment.Author?.Id ?? string.Empty,
                comment.Author?.DisplayName ?? "Unknown",
                comment.Content ?? string.Empty,
                new DateTimeOffset(comment.PublishedDate.ToUniversalTime()),
                comment.IsDeleted == true))
            .ToArray();
        return new FeedbackThread(thread.Id, AzurePullRequestMapper.IsThreadResolved(thread.Status.ToString()), anchor, comments);
    }

    private static WorkItemContext MapWorkItem(
        Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem workItem,
        IEnumerable<AzureWorkItemComment> comments)
    {
        var relations = workItem.Relations ?? [];
        return new WorkItemContext(
            workItem.Id ?? 0,
            GetField(workItem, "System.Title"),
            GetField(workItem, "System.State"),
            GetField(workItem, "System.Description"),
            comments.Select(comment => new Farm.Core.Chickens.WorkItemComment(
                comment.Id,
                comment.CreatedBy?.DisplayName,
                comment.Text ?? string.Empty,
                new DateTimeOffset(comment.CreatedDate.ToUniversalTime()))).ToArray(),
            relations.Select(relation => new Farm.Core.Chickens.WorkItemRelation(
                relation.Rel,
                new Uri(relation.Url),
                GetAttribute(relation, "name"))).ToArray(),
            relations.Where(relation => string.Equals(relation.Rel, "AttachedFile", StringComparison.OrdinalIgnoreCase))
                .Select(relation => new WorkItemAttachment(
                    GetAttribute(relation, "name") ?? "attachment",
                    new Uri(relation.Url),
                    GetAttribute(relation, "comment")))
                .ToArray());
    }

    private static string? GetField(Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem workItem, string name) =>
        workItem.Fields.TryGetValue(name, out var value) ? value?.ToString() : null;

    private static string? GetAttribute(AzureWorkItemRelation relation, string name) =>
        relation.Attributes is not null && relation.Attributes.TryGetValue(name, out var value) ? value?.ToString() : null;

    [GeneratedRegex("^[0-9a-fA-F]{40}$", RegexOptions.CultureInvariant)]
    private static partial Regex FullCommitSha();
}