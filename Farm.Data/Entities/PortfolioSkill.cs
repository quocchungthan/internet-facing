namespace Farm.Data.Entities;

public class PortfolioSkill
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;

    public ICollection<ProfileSkill> Profiles { get; set; } = new List<ProfileSkill>();
    public ICollection<ProjectSkill> ProjectSkills { get; set; } = new List<ProjectSkill>();
}
