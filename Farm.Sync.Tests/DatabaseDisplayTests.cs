using AffineSync;
using Xunit;

namespace AffineSync.Tests;

public sealed class DatabaseDisplayTests
{
    [Fact]
    public void RendersAlignedRowsWithoutTerminalControls()
    {
        using var output = new StringWriter();
        DatabaseDisplay.WriteTable(output, "Example", ["Id", "Value"],
            [new object?[] { 1, "line\nbreak\u001b" }, new object?[] { 2, true }]);
        var text = output.ToString();
        Assert.Contains("| 1  | line break  |", text);
        Assert.Contains("true", text);
        Assert.Contains("2 row(s)", text);
        Assert.DoesNotContain('\u001b', text);
        var lines = text.Split(Environment.NewLine).Where(line => line.StartsWith('|')).ToList();
        Assert.Single(lines.Select(line => line.Length).Distinct());
    }

    [Fact]
    public void RendersEmptyTablesWithHeaders()
    {
        using var output = new StringWriter();
        DatabaseDisplay.WriteTable(output, "Empty", ["Id"], []);
        Assert.Contains("| Id |", output.ToString());
        Assert.Contains("0 row(s)", output.ToString());
    }

    [Fact]
    public void RejectsMismatchedColumnCounts()
    {
        using var output = new StringWriter();
        Assert.Throws<ArgumentException>(() =>
            DatabaseDisplay.WriteTable(output, "Invalid", ["Id"], [new object?[] { 1, 2 }]));
    }
}
