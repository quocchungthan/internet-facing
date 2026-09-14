using System.Net.Http.Headers;
using System.Text.Json;
using Fences.Models;
using Microsoft.AspNetCore.Http;

namespace Fences.Services;

public interface IGitHubRepositoryService
{
    Task<(IReadOnlyList<GitHubRepositoryRecord> Repositories, string? ErrorMessage, int? StatusCode)> GetRepositoriesAsync(string accessToken, int perPage, CancellationToken cancellationToken);
    Task<(GitHubRepositoryRecord? Repository, bool Created, string? ErrorMessage, int? StatusCode)> CreateRepositoryAsync(string accessToken, string ownerLogin, string name, bool isPrivate, string? description, CancellationToken cancellationToken);
}

public sealed class GitHubRepositoryService(IHttpClientFactory httpClientFactory) : IGitHubRepositoryService
{
    private static HttpRequestMessage CreateGitHubRequest(HttpMethod method, string url, string accessToken)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.UserAgent.ParseAdd("fences-identity-app");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return request;
    }

    private static GitHubRepositoryRecord ParseRepository(JsonElement repo) => new(
        Id: repo.GetProperty("id").GetInt64().ToString(),
        Name: repo.GetProperty("name").GetString() ?? string.Empty,
        FullName: repo.GetProperty("full_name").GetString() ?? string.Empty,
        IsPrivate: repo.GetProperty("private").GetBoolean(),
        DefaultBranch: repo.GetProperty("default_branch").GetString(),
        HtmlUrl: repo.GetProperty("html_url").GetString(),
        CloneUrl: repo.GetProperty("clone_url").GetString(),
        PushedAt: repo.TryGetProperty("pushed_at", out var pushedAt) ? pushedAt.GetString() : null);

    public async Task<(IReadOnlyList<GitHubRepositoryRecord> Repositories, string? ErrorMessage, int? StatusCode)> GetRepositoriesAsync(string accessToken, int perPage, CancellationToken cancellationToken)
    {
        var normalizedPerPage = Math.Clamp(perPage, 1, 100);
        var client = httpClientFactory.CreateClient();
        using var request = CreateGitHubRequest(HttpMethod.Get, $"https://api.github.com/user/repos?sort=updated&visibility=all&per_page={normalizedPerPage}", accessToken);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return (Array.Empty<GitHubRepositoryRecord>(), payload, (int)response.StatusCode);
        }

        using var jsonDoc = JsonDocument.Parse(payload);
        var repositories = jsonDoc.RootElement.EnumerateArray().Select(ParseRepository).ToArray();
        return (repositories, null, null);
    }

    public async Task<(GitHubRepositoryRecord? Repository, bool Created, string? ErrorMessage, int? StatusCode)> CreateRepositoryAsync(string accessToken, string ownerLogin, string name, bool isPrivate, string? description, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient();
        var encodedOwner = Uri.EscapeDataString(ownerLogin);
        var encodedName = Uri.EscapeDataString(name);

        using (var lookupRequest = CreateGitHubRequest(HttpMethod.Get, $"https://api.github.com/repos/{encodedOwner}/{encodedName}", accessToken))
        using (var lookupResponse = await client.SendAsync(lookupRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
        {
            if (lookupResponse.IsSuccessStatusCode)
            {
                var payload = await lookupResponse.Content.ReadAsStringAsync(cancellationToken);
                using var jsonDoc = JsonDocument.Parse(payload);
                return (ParseRepository(jsonDoc.RootElement), false, null, null);
            }

            if ((int)lookupResponse.StatusCode != StatusCodes.Status404NotFound)
            {
                var lookupPayload = await lookupResponse.Content.ReadAsStringAsync(cancellationToken);
                return (null, false, lookupPayload, (int)lookupResponse.StatusCode);
            }
        }

        using var createRequest = CreateGitHubRequest(HttpMethod.Post, "https://api.github.com/user/repos", accessToken);
        createRequest.Content = new StringContent(JsonSerializer.Serialize(new { name, @private = isPrivate, description }));
        createRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        using var createResponse = await client.SendAsync(createRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var createPayload = await createResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!createResponse.IsSuccessStatusCode)
        {
            return (null, false, createPayload, (int)createResponse.StatusCode);
        }

        using var createJsonDoc = JsonDocument.Parse(createPayload);
        return (ParseRepository(createJsonDoc.RootElement), true, null, null);
    }
}
