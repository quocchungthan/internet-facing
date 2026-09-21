using Farm.Azure;
using Farm.Core.Contracts;
using System.Net.Http;
using Microsoft.VisualStudio.Services.Common;

public static class ConsoleApp
{
    public static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };
        Console.CancelKeyPress += cancelHandler;

        try
        {
            var command = args.FirstOrDefault()?.ToLowerInvariant();
            if (command is not "work-items")
            {
                Console.Error.WriteLine("Usage: Farm.Console work-items <id> [<id> ...]");
                return 2;
            }

            if (args.Length < 2 || !args.Skip(1).All(value => int.TryParse(value, out var id) && id > 0))
            {
                Console.Error.WriteLine("At least one positive numeric work item ID is required.");
                return 2;
            }

            var ids = args.Skip(1).Select(int.Parse).ToArray();
            if (ids.Distinct().Count() != ids.Length)
            {
                Console.Error.WriteLine("Work item IDs must not contain duplicates.");
                return 2;
            }

            var settings = AzureDevOpsSettingsLoader.LoadFromEnvironment();
            using var client = new AzureDevOpsClient(settings);
            IWorkItemSource workItemSource = new AzureWorkItemSource(client);
            var workItems = await workItemSource.GetWorkItemsAsync(ids, cancellation.Token);

            foreach (var workItem in workItems)
            {
                Console.WriteLine($"{workItem.Id}: {workItem.Title} [{workItem.State}]");
            }

            return 0;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            Console.Error.WriteLine("Operation cancelled.");
            return 130;
        }
        catch (Exception exception) when (exception is ArgumentException
            or InvalidOperationException
            or UriFormatException
            or HttpRequestException
            or VssException)
        {
            Console.Error.WriteLine($"Error: {exception.Message}");
            return 1;
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
        }
    }
}