using System.ComponentModel.DataAnnotations;
using Farm.Data.Entities;

namespace Farm.Models;

public sealed class PortfolioPage
{
    [Range(1, 10000)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 50;
}

// Flat scalar contracts deliberately omit navigation properties and private ContactEmail.
public sealed record ProfileResponse
{
    public int Id { get; init; }
    public required string DisplayName { get; init; }
    public required string Headline { get; init; }
    public string? Bio { get; init; }
    public string? Location { get; init; }
    public string? ProfileImageUrl { get; init; }
    public string? AvatarSvg { get; init; }
    public string? ResumeUrl { get; init; }
}

public sealed record ProjectResponse
{
    public int Id { get; init; }
    public required string Title { get; init; }
    public required string Slug { get; init; }
    public required string Summary { get; init; }
    public string? Description { get; init; }
    public string? DemoUrl { get; init; }
    public string? SourceUrl { get; init; }
}

public sealed record SkillResponse
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public required string Category { get; init; }
}

public sealed record ExperienceResponse
{
    public int Id { get; init; }
    public int ProfileId { get; init; }
    public required string Company { get; init; }
    public required string JobTitle { get; init; }
    public string? Description { get; init; }
    public EmploymentType EmploymentType { get; init; }
    public WorkMode WorkMode { get; init; }
    public DateOnly StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public bool IsCurrent { get; init; }
    public int DisplayOrder { get; init; }
}

public sealed record SocialLinkResponse
{
    public int Id { get; init; }
    public int ProfileId { get; init; }
    public required string Label { get; init; }
    public required string Url { get; init; }
    public int DisplayOrder { get; init; }
}

public sealed record ProfileProjectResponse
{
    public int ProfileId { get; init; }
    public int ProjectId { get; init; }
    public string? Role { get; init; }
    public string? Organization { get; init; }
    public DateOnly? StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public bool IsFeatured { get; init; }
    public int DisplayOrder { get; init; }
}

public sealed record ProfileSkillResponse
{
    public int ProfileId { get; init; }
    public int SkillId { get; init; }
    public int DisplayOrder { get; init; }
}

public sealed record ProjectSkillResponse
{
    public int ProjectId { get; init; }
    public int SkillId { get; init; }
}
