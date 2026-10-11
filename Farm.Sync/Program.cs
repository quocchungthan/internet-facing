using AffineSync;
using Farm.Data;
using Microsoft.EntityFrameworkCore;

if (args.Any(argument => argument is not ("--once" or "--show-data")) ||
    (args.Contains("--once") && args.Contains("--show-data")))
{
    Console.Error.WriteLine("Usage: AffineSync [--once | --show-data]");
    return 1;
}

using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) => { eventArgs.Cancel = true; shutdown.Cancel(); };
using var terminate = System.Runtime.InteropServices.PosixSignalRegistration.Create(
    System.Runtime.InteropServices.PosixSignal.SIGTERM, signal => { signal.Cancel = true; shutdown.Cancel(); });
try
{
    if (args.Contains("--show-data"))
    {
        var connectionString = Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("DATABASE_CONNECTION_STRING is required.");
        var readOptions = new DbContextOptionsBuilder<AuraFarming>().UseNpgsql(connectionString).Options;
        await using var database = new AuraFarming(readOptions);
        await DatabaseDisplay.WriteAsync(database, Console.Out, shutdown.Token);
        return 0;
    }
    var configuration = WorkerConfiguration.FromEnvironment();
    var state = new CheckpointStore(configuration.StatePath);
    using var ownership = state.AcquireLock();
    var options = new DbContextOptionsBuilder<AuraFarming>()
        .UseNpgsql(configuration.ConnectionString).Options;
    await using (var database = new AuraFarming(options))
        await database.Database.MigrateAsync(shutdown.Token);
    var portfolioOptions = new DbContextOptionsBuilder<PortfolioDbContext>()
        .UseNpgsql(configuration.ConnectionString, provider => provider.MigrationsHistoryTable("__PortfolioMigrationsHistory"))
        .Options;
    await using (var portfolio = new PortfolioDbContext(portfolioOptions))
        await portfolio.Database.MigrateAsync(shutdown.Token);

    var worker = new SyncRunner(new NodeReader(configuration.ReaderPath), state,
        new DatabaseWriter(() => new AuraFarming(options), () => new PortfolioDbContext(portfolioOptions)));
    var once = args.Contains("--once");
    do
    {
        try
        {
            var count = await worker.RunAsync(shutdown.Token);
            Console.WriteLine($"{DateTimeOffset.UtcNow:O} Synced {count} changed document(s).");
        }
        catch (OperationCanceledException) when (shutdown.IsCancellationRequested) { return once ? 1 : 0; }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"{DateTimeOffset.UtcNow:O} Sync failed ({exception.GetType().Name}): {exception.GetBaseException().Message}");
            if (once) return 1;
        }
        if (once) return 0;
        await Task.Delay(TimeSpan.FromSeconds(configuration.IntervalSeconds), shutdown.Token);
    } while (!shutdown.IsCancellationRequested);
    return 0;
}
catch (OperationCanceledException) when (shutdown.IsCancellationRequested) { return args.Contains("--once") ? 1 : 0; }
catch (Exception exception)
{
    Console.Error.WriteLine($"{DateTimeOffset.UtcNow:O} Startup failed ({exception.GetType().Name}): {exception.GetBaseException().Message}");
    return 1;
}
