using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Farm.Data;

public class PortfolioDesignTimeFactory : IDesignTimeDbContextFactory<PortfolioDbContext>
{
    public PortfolioDbContext CreateDbContext(string[] args)
    {
        // Reuse Farm's tooling connection resolution without opening a connection.
        using var farm = new AuraFarmingDesignTimeFactory().CreateDbContext(args);
        var options = new DbContextOptionsBuilder<PortfolioDbContext>()
            .UseNpgsql(farm.Database.GetConnectionString(),
                provider => provider.MigrationsHistoryTable("__PortfolioMigrationsHistory"))
            .Options;
        return new PortfolioDbContext(options);
    }
}
