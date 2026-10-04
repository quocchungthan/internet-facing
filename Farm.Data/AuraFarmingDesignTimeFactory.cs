using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Farm.Data;

public class AuraFarmingDesignTimeFactory : IDesignTimeDbContextFactory<AuraFarming>
{
    public AuraFarming CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AuraFarming>();
        
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__AuraFarming");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            var dbPort = Environment.GetEnvironmentVariable("POSTGRES_PORT") ?? "4554";
            var dbPass = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD") ?? "postgres";
            var csb = new Npgsql.NpgsqlConnectionStringBuilder
            {
                Host = "localhost",
                Port = int.Parse(dbPort),
                Database = "aurafarming",
                Username = "postgres"
            };
            csb["Password"] = dbPass;
            connectionString = csb.ConnectionString;
        }

        optionsBuilder.UseNpgsql(connectionString, b =>
        {
            b.MigrationsAssembly(typeof(AuraFarming).Assembly.GetName().Name);
        });

        return new AuraFarming(optionsBuilder.Options);
    }
}
