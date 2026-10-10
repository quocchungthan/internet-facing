namespace Farm.Data.Entities;

public class ProfileSkill
{
    public int ProfileId { get; set; }
    public int SkillId { get; set; }
    public int DisplayOrder { get; set; }

    public PortfolioProfile Profile { get; set; } = null!;
    public PortfolioSkill Skill { get; set; } = null!;
}
