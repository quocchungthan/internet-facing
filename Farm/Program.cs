using Farm.Data;
using Farm.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod()
              .WithExposedHeaders(
                  "X-Total-Requests",
                  "X-Unique-Clients",
                  "X-Endpoint-Requests",
                  "X-Endpoint-Unique-Clients",
                  "X-RateLimit-Limit",
                  "X-RateLimit-Remaining",
                  "X-RateLimit-Reset",
                  "X-Client-Status"
              );
    });
});

// Configure AuraFarming DbContext
var useInMemory = builder.Configuration.GetValue<bool>("UseInMemoryDatabase")
    || Environment.GetEnvironmentVariable("USE_IN_MEMORY_DATABASE") == "true";

if (useInMemory)
{
    var inMemoryDbName = builder.Configuration["InMemoryDatabaseName"] ?? "AuraFarmingInMemory";
    builder.Services.AddDbContext<AuraFarming>(options =>
    {
        options.UseInMemoryDatabase(inMemoryDbName);
    });
}
else
{
    builder.Services.AddDbContext<AuraFarming>(options =>
    {
        var configuredConnStr = builder.Configuration.GetConnectionString("AuraFarming");
        
        // Resolve connection string from env or config
        var hostEnv = Environment.GetEnvironmentVariable("POSTGRES_HOST");
        var isContainer = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";
        var defaultHost = isContainer ? "host.docker.internal" : "localhost";
        var host = !string.IsNullOrWhiteSpace(hostEnv) ? hostEnv : defaultHost;

        var port = Environment.GetEnvironmentVariable("POSTGRES_PORT") ?? "4554";
        var database = Environment.GetEnvironmentVariable("POSTGRES_DB") ?? "aurafarming";
        var username = Environment.GetEnvironmentVariable("POSTGRES_USER") ?? "postgres";
        var passSecret = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD") ?? "postgres";

        var npgsqlBuilder = new Npgsql.NpgsqlConnectionStringBuilder(
            !string.IsNullOrWhiteSpace(configuredConnStr) && string.IsNullOrWhiteSpace(hostEnv)
                ? configuredConnStr
                : $"Host={host};Port={port};Database={database};Username={username};");
        npgsqlBuilder["Password"] = passSecret;

        var connectionString = npgsqlBuilder.ConnectionString;

        options.UseNpgsql(connectionString, b =>
        {
            b.MigrationsAssembly(typeof(AuraFarming).Assembly.GetName().Name);
        });
    });
}

// Portfolio tables share Farm's provider/connection but have an independent migration history.
builder.Services.AddDbContext<PortfolioDbContext>((services, options) =>
{
    if (useInMemory)
    {
        var name = builder.Configuration["InMemoryDatabaseName"] ?? "AuraFarmingInMemory";
        options.UseInMemoryDatabase(name + "-Portfolio");
    }
    else
    {
        var farm = services.GetRequiredService<AuraFarming>();
        options.UseNpgsql(farm.Database.GetConnectionString(),
            provider => provider.MigrationsHistoryTable("__PortfolioMigrationsHistory"));
    }
});

// Register Client Tracing & Rate Limiting Service
builder.Services.AddSingleton<IClientTracingService, ClientTracingService>();

var app = builder.Build();

// Run EF Migration on Startup in dotnet
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var db = scope.ServiceProvider.GetRequiredService<AuraFarming>();
    try
    {
        if (db.Database.IsRelational())
        {
            logger.LogInformation("Applying AuraFarming database migrations on startup...");
            db.Database.Migrate();
            logger.LogInformation("AuraFarming database migrations applied successfully.");
        }
        else
        {
            logger.LogInformation("Ensuring AuraFarming database is created with seeds...");
            db.Database.EnsureCreated();
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to apply AuraFarming database migrations on startup.");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllers();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

public partial class Program { }
