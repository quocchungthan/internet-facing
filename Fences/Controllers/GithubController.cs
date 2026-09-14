using Fences.Models;
using Fences.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Fences.Controllers;

[ApiController]
[Authorize]
public sealed class GithubController(IGitHubRepositoryService gitHubRepositoryService) : ControllerBase
{
    [HttpGet("/api/github/clone-token")]
    public async Task<IActionResult> GetCloneToken()
    {
        var token = await HttpContext.GetTokenAsync("access_token");
        return string.IsNullOrWhiteSpace(token)
            ? Unauthorized(new { message = "GitHub access token is missing. Re-login is required." })
            : Ok(new { token });
    }

    [HttpGet("/api/github/repos")]
    public async Task<IActionResult> GetRepositories([FromQuery] int perPage = 20)
    {
        var token = await HttpContext.GetTokenAsync("access_token");
        if (string.IsNullOrWhiteSpace(token))
        {
            return Unauthorized(new { message = "GitHub access token is missing. Re-login is required." });
        }

        var result = await gitHubRepositoryService.GetRepositoriesAsync(token, perPage, HttpContext.RequestAborted);
        return string.IsNullOrWhiteSpace(result.ErrorMessage)
            ? Ok(new { count = result.Repositories.Count, repos = result.Repositories })
            : StatusCode(result.StatusCode ?? StatusCodes.Status502BadGateway, new { message = "Failed to query GitHub repositories.", githubResponse = result.ErrorMessage });
    }

    [HttpPost("/api/github/repos")]
    public async Task<IActionResult> CreateRepository([FromBody] CreateGitHubRepositoryRequest request)
    {
        var token = await HttpContext.GetTokenAsync("access_token");
        if (string.IsNullOrWhiteSpace(token))
        {
            return Unauthorized(new { message = "GitHub access token is missing. Re-login is required." });
        }

        var repoName = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(repoName))
        {
            return BadRequest(new { message = "Repository name is required." });
        }

        var githubLogin = User.FindFirstValue("urn:github:login");
        if (string.IsNullOrWhiteSpace(githubLogin))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Authenticated identity is missing GitHub login." });
        }

        var result = await gitHubRepositoryService.CreateRepositoryAsync(token, githubLogin, repoName, request.Private ?? true, string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(), HttpContext.RequestAborted);
        return string.IsNullOrWhiteSpace(result.ErrorMessage)
            ? StatusCode(result.Created ? StatusCodes.Status201Created : StatusCodes.Status200OK, new { created = result.Created, repo = result.Repository })
            : StatusCode(result.StatusCode ?? StatusCodes.Status502BadGateway, new { message = "Failed to create GitHub repository.", githubResponse = result.ErrorMessage });
    }
}
