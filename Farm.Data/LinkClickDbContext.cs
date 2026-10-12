using Farm.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Farm.Data;

public class LinkClickDbContext(DbContextOptions<LinkClickDbContext> options) : DbContext(options)
{
    public DbSet<LinkClickDaily> LinkClicks => Set<LinkClickDaily>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<LinkClickDaily>(entity =>
        {
            entity.ToTable("LinkClickDaily");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Platform).HasMaxLength(64).IsRequired();
            entity.HasIndex(e => new { e.Platform, e.Day }).IsUnique();
        });
    }
}
