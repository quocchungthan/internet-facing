namespace Fences.Models;

public sealed record CreateGitHubRepositoryRequest(
    string Name,
    bool? Private,
    string? Description);
