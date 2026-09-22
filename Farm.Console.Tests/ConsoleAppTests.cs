using Xunit;

namespace Farm.Console.Tests;

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

            Assert.Equal(0, exitCode);
            Assert.Contains("Usage:", output.ToString());
            Assert.Contains("work-items", output.ToString());
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
    [InlineData("pr-threads", "1")]
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
    public async Task RunAsync_commands_requiring_configuration_fail_cleanly_without_environment(string command)
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
            var exitCode = await ConsoleApp.RunAsync([command]);

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
}