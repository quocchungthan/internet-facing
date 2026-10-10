namespace Farm.Data.Entities;

public class PortfolioExperience
{
    public int Id { get; set; }
    public string? SourceDocId { get; set; }
    public int ProfileId { get; set; }
    public string Company { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public string? Description { get; set; }
    public EmploymentType EmploymentType { get; set; }
    public WorkMode WorkMode { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool IsCurrent { get; set; }
    public int DisplayOrder { get; set; }

    public PortfolioProfile Profile { get; set; } = null!;
}
