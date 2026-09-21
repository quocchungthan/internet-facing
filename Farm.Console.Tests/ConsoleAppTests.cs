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
}