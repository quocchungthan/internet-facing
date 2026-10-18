using Microsoft.EntityFrameworkCore;

namespace Fences.Data;

public sealed class AudienceDbContext(DbContextOptions<AudienceDbContext> options) : DbContext(options)
{
    public DbSet<AudienceVisit> Visits => Set<AudienceVisit>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<AudienceVisit>().HasIndex(visit => new { visit.ClientId, visit.Subject, visit.Day }).IsUnique();
    }
}

public sealed class AudienceVisit
{
    public long Id { get; set; }
    public string ClientId { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Username { get; set; } = "";
    public string Day { get; set; } = "";
}
