namespace Farm.Data.Entities;

public class GigProblem
{
    public int Id { get; set; }
    public string? SourceDocId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string? Problem { get; set; }
    public string? Outcome { get; set; }
    public DateOnly? SolvedOn { get; set; }
    public int DisplayOrder { get; set; }

    public ICollection<GigStep> Steps { get; set; } = new List<GigStep>();
    public ICollection<GigProblemSkill> Skills { get; set; } = new List<GigProblemSkill>();
}
