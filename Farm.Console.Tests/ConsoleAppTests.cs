using Farm.Core.Contracts;
using Farm.Core.Domain;
using Microsoft.VisualStudio.Services.Common;
using Spectre.Console;
using Xunit;

namespace Farm.Console.Tests;

[Collection("Console output")]
public sealed class ConsoleAppTests
{
    [Theory]
    [InlineData("0")]
    [InlineData("-4")]
    public async Task RunAsync_rejects_invalid_ids_without_loading_configuration(string id)
    {
        var exitCode = await ConsoleApp.RunAsync(["work-items", id]);

        Assert.Equal(2, exitCode);
    }

    [Fact]
    public async Task RunAsync_rejects_duplicate_ids_without_loading_configuration()
    {
        var exitCode = await ConsoleApp.RunAsync(["work-items", "1", "1"]);

        Assert.Equal(2, exitCode);
    }

    [Fact]
    public async Task RunAsync_with_no_args_prints_help_and_exits_zero()
    {
        var output = new StringWriter();
        var originalOut = System.Console.Out;
        System.Console.SetOut(output);
        try
        {
            var exitCode = await ConsoleApp.RunAsync([]);
            var renderedOutput = output.ToString();

            Assert.Equal(0, exitCode);
            Assert.StartsWith("sam ", renderedOutput);
            Assert.Contains("Usage: sam <command>", renderedOutput);
            Assert.DoesNotContain("Usage: farm ", renderedOutput);
            Assert.Contains("work-items", renderedOutput);
        }
        finally
        {
            System.Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task RunAsync_help_prints_complete_pull_request_command_forms()
    {
        var output = new StringWriter();
        var originalOut = System.Console.Out;
        System.Console.SetOut(output);
        try
        {
            var exitCode = await ConsoleApp.RunAsync(["--help"]);
            var renderedOutput = output.ToString();

            Assert.Equal(0, exitCode);
            Assert.StartsWith("sam ", renderedOutput);
            Assert.Contains("Usage: sam <command>", renderedOutput);
            Assert.DoesNotContain("Usage: farm ", renderedOutput);
            Assert.Contains("pull-requests approved-by-me", renderedOutput);
            Assert.Contains("pull-requests assigned-to-me", renderedOutput);
            Assert.Contains("pull-requests pending-review", renderedOutput);
            Assert.Contains("pull-requests mine", renderedOutput);
        }
        finally
        {
            System.Console.SetOut(originalOut);
        }
    }

    [Theory]
    [InlineData("--version")]
    [InlineData("-v")]
    public async Task RunAsync_version_flag_exits_zero(string flag)
    {
        var exitCode = await ConsoleApp.RunAsync([flag]);

        Assert.Equal(0, exitCode);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    public async Task RunAsync_help_flag_exits_zero(string flag)
    {
        var exitCode = await ConsoleApp.RunAsync([flag]);

        Assert.Equal(0, exitCode);
    }

    [Theory]
    [InlineData("pr-diff", "1")]
    [InlineData("work-item-comments", "1")]
    [InlineData("work-item-relations", "1")]
    public async Task RunAsync_stub_commands_return_not_implemented_exit_code(string command, string id)
    {
        var error = new StringWriter();
        var originalError = System.Console.Error;
        System.Console.SetError(error);
        try
        {
            var exitCode = await ConsoleApp.RunAsync([command, id]);

            Assert.Equal(3, exitCode);
            Assert.Contains("not yet implemented", error.ToString());
        }
        finally
        {
            System.Console.SetError(originalError);
        }
    }

    [Theory]
    [InlineData("pr-threads")]
    [InlineData("pr-diff")]
    [InlineData("work-item-comments")]
    [InlineData("work-item-relations")]
    public async Task RunAsync_stub_commands_reject_invalid_args(string command)
    {
        var exitCode = await ConsoleApp.RunAsync([command, "not-a-number"]);

        Assert.Equal(2, exitCode);
    }

    [Fact]
    public async Task RunAsync_work_items_assigned_to_rejects_missing_assignee()
    {
        var exitCode = await ConsoleApp.RunAsync(["work-items", "assigned-to"]);

        Assert.Equal(2, exitCode);
    }

    [Fact]
    public async Task RunAsync_work_items_assigned_to_rejects_extra_args()
    {
        var exitCode = await ConsoleApp.RunAsync(["work-items", "assigned-to", "me", "extra"]);

        Assert.Equal(2, exitCode);
    }

    [Theory]
    [InlineData("assign")]
    [InlineData("assign 1")]
    [InlineData("assign 0 me")]
    [InlineData("assign -1 person@example.com")]
    [InlineData("assign 1 me extra")]
    [InlineData("unassign")]
    [InlineData("unassign 0")]
    [InlineData("unassign -1")]
    [InlineData("unassign 1 extra")]
    public async Task RunAsync_work_item_mutations_reject_invalid_arguments_without_loading_configuration(
        string arguments)
    {
        var exitCode = await ConsoleApp.RunAsync(["work-items", .. arguments.Split(' ')]);

        Assert.Equal(2, exitCode);
    }

    [Fact]
    public async Task RunAsync_work_item_assign_prints_confirmation_and_updated_details()
    {
        var workItem = CreateWorkItem();
        var assignmentService = new StubWorkItemAssignmentService((id, assignee, _) =>
        {
            Assert.Equal(42, id);
            Assert.Equal("me", assignee);
            return Task.FromResult(workItem);
        });
        var output = new StringWriter();
        var originalOut = System.Console.Out;
        System.Console.SetOut(output);
        AnsiConsole.Record();

        try
        {
            var exitCode = await ConsoleApp.RunAsync(
                ["work-items", "assign", "42", "me"],
                assignmentService);

            Assert.Equal(0, exitCode);
            Assert.Contains("Assigned work item 42 to person@example.com.", output.ToString());
            var details = AnsiConsole.ExportText();
            Assert.Contains("Work Item 42", details);
            Assert.Contains("Assignment test", details);
            Assert.Contains("person@example.com", details);
        }
        finally
        {
            System.Console.SetOut(originalOut);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RunAsync_work_item_assign_returns_one_without_stack_trace_for_expected_failures(
        bool useVssException)
    {
        Exception failure = useVssException
            ? new TestVssException("Azure assignment failed.")
            : new InvalidOperationException("Work item changed during assignment.");
        var assignmentService = new StubWorkItemAssignmentService((_, _, _) => Task.FromException<WorkItem>(failure));
        var error = new StringWriter();
        var originalError = System.Console.Error;
        System.Console.SetError(error);

        try
        {
            var exitCode = await ConsoleApp.RunAsync(
                ["work-items", "assign", "42", "me"],
                assignmentService);

            Assert.Equal(1, exitCode);
            Assert.Equal($"Error: {failure.Message}{Environment.NewLine}", error.ToString());
            Assert.DoesNotContain(nameof(ConsoleAppTests), error.ToString());
            Assert.DoesNotContain(" at ", error.ToString());
        }
        finally
        {
            System.Console.SetError(originalError);
        }
    }

    [Fact]
    public async Task RunAsync_pull_requests_rejects_unknown_subcommand()
    {
        var exitCode = await ConsoleApp.RunAsync(["pull-requests", "not-a-subcommand"]);

        Assert.Equal(2, exitCode);
    }

    [Fact]
    public async Task RunAsync_pull_requests_rejects_extra_args()
    {
        var exitCode = await ConsoleApp.RunAsync(["pull-requests", "approved-by-me", "extra"]);

        Assert.Equal(2, exitCode);
    }

    [Fact]
    public async Task RunAsync_work_items_needs_attention_rejects_extra_args()
    {
        var exitCode = await ConsoleApp.RunAsync(["work-items", "needs-attention", "extra"]);

        Assert.Equal(2, exitCode);
    }

    [Fact]
    public async Task RunAsync_whoami_rejects_arguments()
    {
        var exitCode = await ConsoleApp.RunAsync(["whoami", "extra"]);

        Assert.Equal(2, exitCode);
    }

    [Fact]
    public async Task RunAsync_my_groups_rejects_arguments()
    {
        var exitCode = await ConsoleApp.RunAsync(["my-groups", "extra"]);

        Assert.Equal(2, exitCode);
    }

    [Theory]
    [InlineData("whoami")]
    [InlineData("my-groups")]
    [InlineData("pull-requests")]
    [InlineData("pull-requests assigned-to-me")]
    [InlineData("pull-requests pending-review")]
    [InlineData("pull-requests mine")]
    [InlineData("work-items needs-attention")]
    [InlineData("work-items assign 1 me")]
    [InlineData("work-items unassign 1")]
    [InlineData("pr-threads 1")]
    public async Task RunAsync_commands_requiring_configuration_fail_cleanly_without_environment(string commandLine)
    {
        var organizationUrl = Environment.GetEnvironmentVariable("FARM_AZURE_DEVOPS_ORGANIZATION_URL");
        var project = Environment.GetEnvironmentVariable("FARM_AZURE_DEVOPS_PROJECT");
        var pat = Environment.GetEnvironmentVariable("FARM_AZURE_DEVOPS_PAT");
        Environment.SetEnvironmentVariable("FARM_AZURE_DEVOPS_ORGANIZATION_URL", null);
        Environment.SetEnvironmentVariable("FARM_AZURE_DEVOPS_PROJECT", null);
        Environment.SetEnvironmentVariable("FARM_AZURE_DEVOPS_PAT", null);

        var error = new StringWriter();
        var originalError = System.Console.Error;
        System.Console.SetError(error);
        try
        {
            var exitCode = await ConsoleApp.RunAsync(commandLine.Split(' '));

            Assert.Equal(1, exitCode);
            Assert.Contains("Environment variable", error.ToString());
        }
        finally
        {
            System.Console.SetError(originalError);
            Environment.SetEnvironmentVariable("FARM_AZURE_DEVOPS_ORGANIZATION_URL", organizationUrl);
            Environment.SetEnvironmentVariable("FARM_AZURE_DEVOPS_PROJECT", project);
            Environment.SetEnvironmentVariable("FARM_AZURE_DEVOPS_PAT", pat);
        }
    }

    private static WorkItem CreateWorkItem() =>
        new(
            42,
            3,
            "Assignment test",
            "Active",
            "Task",
            new Identity("person-id", "Example Person", "person@example.com"),
            new Uri("https://example.test/items/42"),
            []);

    private sealed class StubWorkItemAssignmentService(
        Func<int, string, CancellationToken, Task<WorkItem>> assign) : IWorkItemAssignmentService
    {
        public Task<WorkItem> AssignAsync(
            int id,
            string assignee,
            CancellationToken cancellationToken = default) =>
            assign(id, assignee, cancellationToken);

        public Task<WorkItem> UnassignAsync(int id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class TestVssException(string message) : VssException(message);
}

[CollectionDefinition("Console output", DisableParallelization = true)]
public sealed class ConsoleOutputCollection
{
}