using System.Net.Http;
using System.Reflection;
using Farm.Azure;
using Farm.Core.Contracts;
using Microsoft.VisualStudio.Services.Common;

public static class ConsoleApp
{
    private const string ToolName = "farm";
    private const int NotImplementedExitCode = 3;

    private sealed record CommandDefinition(
        string Name,
        string Description,
        string Usage,
        Func<string[], CancellationToken, Task<int>> Handler);

    private static readonly CommandDefinition[] Commands =
    [
        new(
            "work-items",
            "Query one or more Azure DevOps work items by ID, or list work items assigned to a user.",
            "work-items <id> [<id> ...] | work-items assigned-to <email-or-me>",
            RunWorkItemsAsync),
        new(
            "whoami",
            "Print the current Azure DevOps identity (id, display name, unique name).",
            "whoami",
            RunWhoAmIAsync),
        new(
            "my-groups",
            "List the Azure DevOps groups/teams the current user belongs to.",
            "my-groups",
            RunMyGroupsAsync),
        new(
            "pull-requests",
            "List active pull requests, or pull requests approved by the current user.",
            "pull-requests | pull-requests approved-by-me",
            RunPullRequestsAsync),
        new(
            "pr-threads",
            "List discussion threads/comments on a pull request. (not yet implemented)",
            "pr-threads <pr-id>",
            (args, _) => Task.FromResult(RunNotImplementedStub(args, "pull request ID", "pr-threads <pr-id>"))),
        new(
            "pr-diff",
            "Show the file diff for a pull request. (not yet implemented)",
            "pr-diff <pr-id>",
            (args, _) => Task.FromResult(RunNotImplementedStub(args, "pull request ID", "pr-diff <pr-id>"))),
        new(
            "work-item-comments",
            "List comments/discussion on a work item. (not yet implemented)",
            "work-item-comments <id>",
            (args, _) => Task.FromResult(RunNotImplementedStub(args, "work item ID", "work-item-comments <id>"))),
        new(
            "work-item-relations",
            "List related items/attachments for a work item. (not yet implemented)",
            "work-item-relations <id>",
            (args, _) => Task.FromResult(RunNotImplementedStub(args, "work item ID", "work-item-relations <id>"))),
    ];

    public static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        if (args.Length == 0)
        {
            PrintHelp(Console.Out);
            return 0;
        }

        var first = args[0];
        if (IsVersionFlag(first))
        {
            PrintVersion();
            return 0;
        }

        if (IsHelpFlag(first))
        {
            PrintHelp(Console.Out);
            return 0;
        }

        var command = Commands.FirstOrDefault(c => c.Name == first.ToLowerInvariant());
        if (command is null)
        {
            Console.Error.WriteLine($"Unknown command: {first}");
            PrintHelp(Console.Error);
            return 2;
        }

        var rest = args.Skip(1).ToArray();
        if (rest.Any(IsHelpFlag))
        {
            PrintCommandHelp(command);
            return 0;
        }

        if (rest.Any(IsVersionFlag))
        {
            PrintVersion();
            return 0;
        }

        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };
        Console.CancelKeyPress += cancelHandler;

        try
        {
            return await command.Handler(rest, cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            Console.Error.WriteLine("Operation cancelled.");
            return 130;
        }
        catch (Exception exception) when (exception is ArgumentException
            or InvalidOperationException
            or UriFormatException
            or FormatException
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

    private static async Task<int> RunWorkItemsAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length > 0 && string.Equals(args[0], "assigned-to", StringComparison.OrdinalIgnoreCase))
        {
            return await RunWorkItemsAssignedToAsync(args.Skip(1).ToArray(), cancellationToken);
        }

        if (!TryParsePositiveIds(args, "work item ID", out var ids, out var error))
        {
            Console.Error.WriteLine(error);
            Console.Error.WriteLine("Usage: work-items <id> [<id> ...]");
            return 2;
        }

        var settings = AzureDevOpsSettingsLoader.LoadFromEnvironment();
        using var client = new AzureDevOpsClient(settings);
        IWorkItemSource workItemSource = new AzureWorkItemSource(client);
        var workItems = await workItemSource.GetWorkItemsAsync(ids, cancellationToken);

        foreach (var workItem in workItems)
        {
            Console.WriteLine($"{workItem.Id}: {workItem.Title} [{workItem.State}]");
        }

        return 0;
    }

    private static async Task<int> RunWorkItemsAssignedToAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length != 1 || string.IsNullOrWhiteSpace(args[0]))
        {
            Console.Error.WriteLine("Exactly one assignee (email address or 'me') is required.");
            Console.Error.WriteLine("Usage: work-items assigned-to <email-or-me>");
            return 2;
        }

        var settings = AzureDevOpsSettingsLoader.LoadFromEnvironment();
        using var client = new AzureDevOpsClient(settings);
        IWorkItemSource workItemSource = new AzureWorkItemSource(client);
        var workItems = await workItemSource.GetWorkItemsAssignedToAsync(args[0], cancellationToken);

        foreach (var workItem in workItems)
        {
            Console.WriteLine($"{workItem.Id}: {workItem.Title} [{workItem.State}]");
        }

        return 0;
    }

    private static async Task<int> RunWhoAmIAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length != 0)
        {
            Console.Error.WriteLine("whoami takes no arguments.");
            Console.Error.WriteLine("Usage: whoami");
            return 2;
        }

        var settings = AzureDevOpsSettingsLoader.LoadFromEnvironment();
        using var client = new AzureDevOpsClient(settings);
        IIdentityDirectory identityDirectory = new AzureIdentityDirectory(client);
        var currentUser = await identityDirectory.GetCurrentUserAsync(cancellationToken);

        Console.WriteLine($"Id: {currentUser.Id}");
        Console.WriteLine($"Display name: {currentUser.DisplayName}");
        Console.WriteLine($"Unique name: {currentUser.UniqueName}");

        return 0;
    }

    private static async Task<int> RunMyGroupsAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length != 0)
        {
            Console.Error.WriteLine("my-groups takes no arguments.");
            Console.Error.WriteLine("Usage: my-groups");
            return 2;
        }

        var settings = AzureDevOpsSettingsLoader.LoadFromEnvironment();
        using var client = new AzureDevOpsClient(settings);
        IIdentityDirectory identityDirectory = new AzureIdentityDirectory(client);
        var groups = await identityDirectory.GetGroupsForCurrentUserAsync(cancellationToken);

        foreach (var group in groups)
        {
            Console.WriteLine(group.DisplayName);
        }

        return 0;
    }

    private static async Task<int> RunPullRequestsAsync(string[] args, CancellationToken cancellationToken)
    {
        var approvedByMe = args.Length == 1 && string.Equals(args[0], "approved-by-me", StringComparison.OrdinalIgnoreCase);
        if (args.Length > 1 || (args.Length == 1 && !approvedByMe))
        {
            Console.Error.WriteLine("Unknown pull-requests arguments.");
            Console.Error.WriteLine("Usage: pull-requests | pull-requests approved-by-me");
            return 2;
        }

        var settings = AzureDevOpsSettingsLoader.LoadFromEnvironment();
        using var client = new AzureDevOpsClient(settings);
        IPullRequestSource pullRequestSource = new AzurePullRequestSource(client);

        var pullRequests = approvedByMe
            ? await pullRequestSource.ListApprovedByCurrentUserAsync(cancellationToken)
            : await pullRequestSource.ListActiveAsync(cancellationToken);

        foreach (var pullRequest in pullRequests)
        {
            Console.WriteLine($"{pullRequest.Id}: {pullRequest.Title} [{pullRequest.Status}]");
        }

        return 0;
    }

    private static int RunNotImplementedStub(string[] args, string idLabel, string usage)
    {
        if (!TryParseSingleId(args, idLabel, out _, out var error))
        {
            Console.Error.WriteLine(error);
            Console.Error.WriteLine($"Usage: {usage}");
            return 2;
        }

        Console.Error.WriteLine("This command is not yet implemented.");
        return NotImplementedExitCode;
    }

    private static bool TryParsePositiveIds(string[] args, string idLabel, out int[] ids, out string? error)
    {
        ids = [];
        error = null;

        if (args.Length == 0 || !args.All(value => int.TryParse(value, out var id) && id > 0))
        {
            error = $"At least one positive numeric {idLabel} is required.";
            return false;
        }

        var parsed = args.Select(int.Parse).ToArray();
        if (parsed.Distinct().Count() != parsed.Length)
        {
            error = $"{idLabel}s must not contain duplicates.";
            return false;
        }

        ids = parsed;
        return true;
    }

    private static bool TryParseSingleId(string[] args, string idLabel, out int id, out string? error)
    {
        id = 0;
        error = null;

        if (args.Length != 1 || !int.TryParse(args[0], out id) || id <= 0)
        {
            error = $"Exactly one positive numeric {idLabel} is required.";
            return false;
        }

        return true;
    }

    private static bool IsHelpFlag(string value) => value is "--help" or "-h";

    private static bool IsVersionFlag(string value) => value is "--version" or "-v";

    private static string GetVersion() =>
        Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "0.0.0-dev";

    private static void PrintVersion() => Console.WriteLine($"{ToolName} {GetVersion()}");

    private static void PrintHelp(TextWriter writer)
    {
        writer.WriteLine($"{ToolName} {GetVersion()}");
        writer.WriteLine();
        writer.WriteLine($"Usage: {ToolName} <command> [args] [--help|-h] [--version|-v]");
        writer.WriteLine();
        writer.WriteLine("Commands:");
        foreach (var command in Commands)
        {
            writer.WriteLine($"  {command.Usage,-40} {command.Description}");
        }

        writer.WriteLine();
        writer.WriteLine("Run with no arguments to see this help. Use '<command> --help' for command-specific usage.");
    }

    private static void PrintCommandHelp(CommandDefinition command)
    {
        Console.WriteLine($"{ToolName} {GetVersion()}");
        Console.WriteLine();
        Console.WriteLine($"Usage: {command.Usage}");
        Console.WriteLine(command.Description);
    }
}