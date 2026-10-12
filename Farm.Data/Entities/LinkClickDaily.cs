namespace Farm.Data.Entities;

/// <summary>Number of outbound link clicks for one platform on one UTC day.</summary>
public class LinkClickDaily
{
    public int Id { get; set; }
    public string Platform { get; set; } = string.Empty;
    public DateOnly Day { get; set; }
    public int Count { get; set; }
}
