namespace Fences.Models;

public sealed record GitHubRepositoryRecord(
    string Id,
    string Name,
    string FullName,
    bool IsPrivate,
    string? DefaultBranch,
    string? HtmlUrl,
    string? CloneUrl,
    string? PushedAt);
