namespace Farm.Data.Entities;

public class ProfileProject
{
    public int ProfileId { get; set; }
    public int ProjectId { get; set; }
    public string? Role { get; set; }
    public string? Organization { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool IsFeatured { get; set; }
    public int DisplayOrder { get; set; }

    public PortfolioProfile Profile { get; set; } = null!;
    public PortfolioProject Project { get; set; } = null!;
}
