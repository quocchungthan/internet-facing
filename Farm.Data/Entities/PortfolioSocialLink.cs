namespace Farm.Data.Entities;

public class PortfolioSocialLink
{
    public int Id { get; set; }
    public int ProfileId { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }

    public PortfolioProfile Profile { get; set; } = null!;
}
