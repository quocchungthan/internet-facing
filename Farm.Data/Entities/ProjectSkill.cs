namespace Farm.Data.Entities;

public class ProjectSkill
{
    public int ProjectId { get; set; }
    public int SkillId { get; set; }

    public PortfolioProject Project { get; set; } = null!;
    public PortfolioSkill Skill { get; set; } = null!;
}
