namespace Farm.Data.Entities;

public class WebsiteMetadata
{
    public int Id { get; set; }
    public string DeveloperName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RepositoryUrl { get; set; } = string.Empty;
    public string? SpecifiedIssueUrl { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
