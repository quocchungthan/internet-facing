namespace Farm.Data.Entities;

public class PortfolioProject
{
    public int Id { get; set; }
    public string? SourceDocId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? DemoUrl { get; set; }
    public string? SourceUrl { get; set; }

    public ICollection<ProfileProject> Profiles { get; set; } = new List<ProfileProject>();
    public ICollection<ProjectSkill> ProjectSkills { get; set; } = new List<ProjectSkill>();
}
