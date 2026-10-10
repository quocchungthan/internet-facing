using System.ComponentModel.DataAnnotations;
using Farm.Data;
using Farm.Models;
using Farm.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Farm.Controllers;

[ApiController]
[Route("api/portfolio")]
public class PortfolioController(PortfolioDbContext db, IClientTracingService tracing) : ControllerBase
{
    private IQueryable<ProfileResponse> Profiles => db.Profiles.AsNoTracking()
        .Select(p => new ProfileResponse { Id = p.Id, DisplayName = p.DisplayName,
            Headline = p.Headline, Bio = p.Bio, Location = p.Location,
            ProfileImageUrl = p.ProfileImageUrl, AvatarSvg = p.AvatarSvg, ResumeUrl = p.ResumeUrl });
    private IQueryable<ProjectResponse> Projects => db.Projects.AsNoTracking()
        .Select(p => new ProjectResponse { Id = p.Id, Title = p.Title, Slug = p.Slug,
            Summary = p.Summary, Description = p.Description, DemoUrl = p.DemoUrl, SourceUrl = p.SourceUrl });
    private IQueryable<SkillResponse> Skills => db.Skills.AsNoTracking()
        .Select(p => new SkillResponse { Id = p.Id, Name = p.Name, Category = p.Category });
    private IQueryable<ExperienceResponse> Experiences => db.Experiences.AsNoTracking()
        .Select(p => new ExperienceResponse { Id = p.Id, ProfileId = p.ProfileId, Company = p.Company,
            JobTitle = p.JobTitle, Description = p.Description, EmploymentType = p.EmploymentType,
            WorkMode = p.WorkMode, StartDate = p.StartDate, EndDate = p.EndDate,
            IsCurrent = p.IsCurrent, DisplayOrder = p.DisplayOrder });
    private IQueryable<SocialLinkResponse> SocialLinks => db.SocialLinks.AsNoTracking()
        .Select(p => new SocialLinkResponse { Id = p.Id, ProfileId = p.ProfileId,
            Label = p.Label, Url = p.Url, DisplayOrder = p.DisplayOrder });
    private IQueryable<ProfileProjectResponse> ProfileProjects => db.ProfileProjects.AsNoTracking()
        .Select(p => new ProfileProjectResponse { ProfileId = p.ProfileId, ProjectId = p.ProjectId,
            Role = p.Role, Organization = p.Organization, StartDate = p.StartDate,
            EndDate = p.EndDate, IsFeatured = p.IsFeatured, DisplayOrder = p.DisplayOrder });
    private IQueryable<ProfileSkillResponse> ProfileSkills => db.ProfileSkills.AsNoTracking()
        .Select(p => new ProfileSkillResponse { ProfileId = p.ProfileId, SkillId = p.SkillId,
            DisplayOrder = p.DisplayOrder });
    private IQueryable<ProjectSkillResponse> ProjectSkills => db.ProjectSkills.AsNoTracking()
        .Select(p => new ProjectSkillResponse { ProjectId = p.ProjectId, SkillId = p.SkillId });

    [HttpGet("profiles")]
    public Task<IActionResult> GetProfiles([FromQuery] PortfolioPage pagination, CancellationToken token) =>
        ListAsync(Profiles.OrderBy(p => p.Id), pagination, token);

    [HttpGet("profiles/{id:int}")]
    public Task<IActionResult> GetProfile([Range(1, int.MaxValue)] int id, CancellationToken token) =>
        OneAsync(Profiles.Where(p => p.Id == id), token);

    [HttpGet("projects")]
    public Task<IActionResult> GetProjects([FromQuery] PortfolioPage pagination, CancellationToken token) =>
        ListAsync(Projects.OrderBy(p => p.Id), pagination, token);

    [HttpGet("projects/{id:int}")]
    public Task<IActionResult> GetProject([Range(1, int.MaxValue)] int id, CancellationToken token) =>
        OneAsync(Projects.Where(p => p.Id == id), token);

    [HttpGet("skills")]
    public Task<IActionResult> GetSkills([FromQuery] PortfolioPage pagination, CancellationToken token) =>
        ListAsync(Skills.OrderBy(p => p.Id), pagination, token);

    [HttpGet("skills/{id:int}")]
    public Task<IActionResult> GetSkill([Range(1, int.MaxValue)] int id, CancellationToken token) =>
        OneAsync(Skills.Where(p => p.Id == id), token);

    [HttpGet("experiences")]
    public Task<IActionResult> GetExperiences([FromQuery] PortfolioPage pagination, CancellationToken token) =>
        ListAsync(Experiences.OrderBy(p => p.Id), pagination, token);

    [HttpGet("experiences/{id:int}")]
    public Task<IActionResult> GetExperience([Range(1, int.MaxValue)] int id, CancellationToken token) =>
        OneAsync(Experiences.Where(p => p.Id == id), token);

    [HttpGet("social-links")]
    public Task<IActionResult> GetSocialLinks([FromQuery] PortfolioPage pagination, CancellationToken token) =>
        ListAsync(SocialLinks.OrderBy(p => p.Id), pagination, token);

    [HttpGet("social-links/{id:int}")]
    public Task<IActionResult> GetSocialLink([Range(1, int.MaxValue)] int id, CancellationToken token) =>
        OneAsync(SocialLinks.Where(p => p.Id == id), token);

    [HttpGet("profile-projects")]
    public Task<IActionResult> GetProfileProjects([FromQuery] PortfolioPage pagination, CancellationToken token) =>
        ListAsync(ProfileProjects.OrderBy(p => p.ProfileId).ThenBy(p => p.ProjectId), pagination, token);

    [HttpGet("profile-projects/{profileId:int}/{projectId:int}")]
    public Task<IActionResult> GetProfileProject([Range(1, int.MaxValue)] int profileId,
        [Range(1, int.MaxValue)] int projectId, CancellationToken token) =>
        OneAsync(ProfileProjects.Where(p => p.ProfileId == profileId && p.ProjectId == projectId), token);

    [HttpGet("profile-skills")]
    public Task<IActionResult> GetProfileSkills([FromQuery] PortfolioPage pagination, CancellationToken token) =>
        ListAsync(ProfileSkills.OrderBy(p => p.ProfileId).ThenBy(p => p.SkillId), pagination, token);

    [HttpGet("profile-skills/{profileId:int}/{skillId:int}")]
    public Task<IActionResult> GetProfileSkill([Range(1, int.MaxValue)] int profileId,
        [Range(1, int.MaxValue)] int skillId, CancellationToken token) =>
        OneAsync(ProfileSkills.Where(p => p.ProfileId == profileId && p.SkillId == skillId), token);

    [HttpGet("project-skills")]
    public Task<IActionResult> GetProjectSkills([FromQuery] PortfolioPage pagination, CancellationToken token) =>
        ListAsync(ProjectSkills.OrderBy(p => p.ProjectId).ThenBy(p => p.SkillId), pagination, token);

    [HttpGet("project-skills/{projectId:int}/{skillId:int}")]
    public Task<IActionResult> GetProjectSkill([Range(1, int.MaxValue)] int projectId,
        [Range(1, int.MaxValue)] int skillId, CancellationToken token) =>
        OneAsync(ProjectSkills.Where(p => p.ProjectId == projectId && p.SkillId == skillId), token);

    private async Task<IActionResult> ListAsync<T>(IOrderedQueryable<T> query,
        PortfolioPage page, CancellationToken token)
    {
        var limited = CheckRateLimit();
        if (limited is not null)
            return limited;

        return Ok(await query.Skip((page.Page - 1) * page.PageSize)
            .Take(page.PageSize).ToListAsync(token));
    }

    private async Task<IActionResult> OneAsync<T>(IQueryable<T> query, CancellationToken token)
        where T : class
    {
        var limited = CheckRateLimit();
        if (limited is not null)
            return limited;

        var item = await query.FirstOrDefaultAsync(token);
        return item is null ? NotFound() : Ok(item);
    }

    private IActionResult? CheckRateLimit()
    {
        // Use the route template, not arbitrary IDs/query strings, as the metrics key.
        var endpoint = "/" + ControllerContext.ActionDescriptor.AttributeRouteInfo!.Template;
        var result = tracing.ProcessRequest(HttpContext, endpoint);
        return result.IsAllowed ? null : StatusCode(StatusCodes.Status429TooManyRequests, new
        {
            error = "Too Many Requests",
            message = $"Rate limit of {result.Limit} requests exceeded. Please retry in {result.ResetSeconds} seconds.",
            rateLimit = result
        });
    }
}
