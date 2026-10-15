using AffineSync;
using Farm.Data;
using Microsoft.EntityFrameworkCore;

if (args.Any(argument => argument is not ("--once" or "--show-data" or "--healthcheck" or "--check" or "--show-ids" or "--verify-gigs")) ||
    args.Length > 1 && !(args.Contains("--check") && args.Length == 2 && args.Contains("--show-ids")) ||
    args.Contains("--show-ids") && !args.Contains("--check"))
{
    Console.Error.WriteLine("Usage: AffineSync [--once | --show-data | --healthcheck | --check [--show-ids] | --verify-gigs]");
    return 1;
}

using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) => { eventArgs.Cancel = true; shutdown.Cancel(); };
using var terminate = System.Runtime.InteropServices.PosixSignalRegistration.Create(
    System.Runtime.InteropServices.PosixSignal.SIGTERM, signal => { signal.Cancel = true; shutdown.Cancel(); });
try
{
    if (args.Contains("--verify-gigs"))
    {
        var connectionString = Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("DATABASE_CONNECTION_STRING is required.");
        var readerPath = Environment.GetEnvironmentVariable("AFFINE_READER_PATH") ?? "reader/export.mjs";
        var snapshot = await new NodeReader(readerPath).ReadAsync(shutdown.Token);
        var readOptions = new DbContextOptionsBuilder<PortfolioDbContext>().UseNpgsql(connectionString).Options;
        await using var database = new PortfolioDbContext(readOptions);
        await GigBodyVerification.VerifyAsync(database, snapshot, Console.Out, shutdown.Token);
        return 0;
    }
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
    if (args.Contains("--check"))
        return await SetupCheck.RunAsync(Environment.GetEnvironmentVariable("AFFINE_READER_PATH") ?? "reader/export.mjs",
            args.Contains("--show-ids"), shutdown.Token);
    if (args.Contains("--healthcheck"))
    {
        var path = Environment.GetEnvironmentVariable("SYNC_STATE_PATH") ?? "/state/checkpoint.json";
        var seconds = int.TryParse(Environment.GetEnvironmentVariable("SYNC_INTERVAL_SECONDS"), out var parsed) && parsed > 0 ? parsed : 60;
        return new SyncHealth(path).IsHealthy(DateTimeOffset.UtcNow, SyncHealth.MaxAge(seconds)) ? 0 : 1;
    }
    var configuration = WorkerConfiguration.FromEnvironment();
    var health = new SyncHealth(configuration.StatePath);
    var failures = 0;
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
            failures = 0;
            health.MarkSuccess(DateTimeOffset.UtcNow);
        }
        catch (OperationCanceledException) when (shutdown.IsCancellationRequested) { return once ? 1 : 0; }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"{DateTimeOffset.UtcNow:O} Sync failed ({exception.GetType().Name}): {exception.GetBaseException().Message}");
            if (once) return 1;
            failures++;
        }
        if (once) return 0;
        await Task.Delay(SyncHealth.Backoff(configuration.IntervalSeconds, failures), shutdown.Token);
    } while (!shutdown.IsCancellationRequested);
    return 0;
}
catch (OperationCanceledException) when (shutdown.IsCancellationRequested) { return args.Contains("--once") ? 1 : 0; }
catch (Exception exception)
{
    Console.Error.WriteLine($"{DateTimeOffset.UtcNow:O} Startup failed ({exception.GetType().Name}): {exception.GetBaseException().Message}");
    return 1;
}
