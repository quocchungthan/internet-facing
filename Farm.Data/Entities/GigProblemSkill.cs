namespace Farm.Data.Entities;

public class GigProblemSkill
{
    public int ProblemId { get; set; }
    public int SkillId { get; set; }

    public GigProblem Problem { get; set; } = null!;
    public PortfolioSkill Skill { get; set; } = null!;
}
