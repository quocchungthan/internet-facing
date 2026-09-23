using Microsoft.Extensions.Hosting;
using Farm.Core.Chickens;

namespace Farm.Sandbox.Chickens;

public sealed class ChickenWorker(
    ChickenRunner runner,
    ChickenOptions options,
    ISensitiveDataRedactor redactor,
    ILogger<ChickenWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await FixedDelayScheduler.RunAsync(
            RunSafelyAsync,
            options.SchedulePeriod,
            options.RunImmediately,
            stoppingToken);
    }

    private async Task RunSafelyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await runner.RunOnceAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                "Farm.Sandbox.Chickens cycle failed; next cycle remains scheduled. {Error}",
                redactor.Redact(exception.ToString()));
        }
    }
}