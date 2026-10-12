using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Farm.Data;

public class LinkClickDesignTimeFactory : IDesignTimeDbContextFactory<LinkClickDbContext>
{
    public LinkClickDbContext CreateDbContext(string[] args)
    {
        using var farm = new AuraFarmingDesignTimeFactory().CreateDbContext(args);
        var options = new DbContextOptionsBuilder<LinkClickDbContext>()
            .UseNpgsql(farm.Database.GetConnectionString(),
                provider => provider.MigrationsHistoryTable("__LinkClickMigrationsHistory"))
            .Options;
        return new LinkClickDbContext(options);
    }
}
