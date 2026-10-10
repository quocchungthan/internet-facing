namespace Farm.Data.Entities;

public class GigStep
{
    public int Id { get; set; }
    public int ProblemId { get; set; }
    public int StepNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    public GigProblem Problem { get; set; } = null!;
}
