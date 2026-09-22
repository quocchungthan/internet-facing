using Farm.Azure;
using Farm.Core.Domain;
using Spectre.Console;

internal static class ConsoleTables
{
    public static void RenderWorkItems(IReadOnlyList<WorkItem> workItems, bool needsAttention = false)
    {
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("ID");
        table.AddColumn("Title");
        table.AddColumn("Type");
        table.AddColumn("State");
        table.AddColumn("Assigned To");

        foreach (var workItem in workItems)
        {
            var assignedTo = workItem.AssignedTo is null
                ? "[red]unassigned[/]"
                : EscapeCell(workItem.AssignedTo.DisplayName);
            table.AddRow(
                workItem.Id.ToString(),
                EscapeCell(workItem.Title),
                EscapeCell(workItem.WorkItemType),
                EscapeCell(workItem.State),
                assignedTo);
        }

        Render(table, workItems.Count, needsAttention ? "work items needing attention" : "work items");
    }

    public static void RenderPullRequests(IReadOnlyList<PullRequestSummary> pullRequests)
    {
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("ID");
        table.AddColumn("Title");
        table.AddColumn("Status");
        table.AddColumn("Author");
        table.AddColumn("Reviewers");

        foreach (var pullRequest in pullRequests)
        {
            table.AddRow(
                pullRequest.Id.ToString(),
                EscapeCell(pullRequest.Title),
                StatusMarkup(pullRequest.Status),
                EscapeCell(pullRequest.CreatedBy?.DisplayName),
                EscapeCell(FormatReviewers(pullRequest.Reviewers)));
        }

        Render(table, pullRequests.Count, "pull requests");
    }

    public static void RenderThreads(IReadOnlyList<PullRequestThread> threads)
    {
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Thread ID");
        table.AddColumn("Status");
        table.AddColumn("Comments");
        table.AddColumn("Preview");

        foreach (var thread in threads)
        {
            var resolved = AzurePullRequestMapper.IsThreadResolved(thread.Status);
            var statusMarkup = resolved ? $"[green]{EscapeCell(thread.Status)}[/]" : $"[red]{EscapeCell(thread.Status)}[/]";
            var preview = thread.Comments.FirstOrDefault()?.Content ?? string.Empty;
            table.AddRow(
                thread.Id.ToString(),
                statusMarkup,
                thread.Comments.Count.ToString(),
                EscapeCell(Truncate(preview, 80)));
        }

        Render(table, threads.Count, "discussion threads");
    }

    private static string StatusMarkup(string status) => status switch
    {
        "Completed" => $"[green]{EscapeCell(status)}[/]",
        "Abandoned" => $"[red]{EscapeCell(status)}[/]",
        _ => $"[yellow]{EscapeCell(status)}[/]"
    };

    internal static string EscapeCell(string? value) => Markup.Escape(value ?? string.Empty);

    private static string FormatReviewers(IReadOnlyList<PullRequestReviewer> reviewers) =>
        reviewers.Count == 0
            ? "-"
            : string.Join(", ", reviewers.Select(reviewer => $"{reviewer.Identity.DisplayName} ({VoteLabel(reviewer.Vote)})"));

    private static string VoteLabel(int vote) => vote switch
    {
        10 => "approved",
        5 => "approved w/ suggestions",
        0 => "no vote",
        -5 => "waiting",
        -10 => "rejected",
        _ => vote.ToString()
    };

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : string.Concat(value.AsSpan(0, maxLength - 1), "…");

    private static void Render(Table table, int count, string label)
    {
        if (count == 0)
        {
            AnsiConsole.MarkupLine($"[grey]No {Markup.Escape(label)} found.[/]");
            return;
        }

        AnsiConsole.Write(table);
    }
}
