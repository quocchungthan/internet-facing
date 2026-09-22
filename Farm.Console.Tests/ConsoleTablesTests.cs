using Xunit;

namespace Farm.Console.Tests;

public sealed class ConsoleTablesTests
{
    [Fact]
    public void EscapeCell_escapes_brackets_in_titles_and_display_names()
    {
        var escaped = ConsoleTables.EscapeCell("[Feature] assigned to [Developer]");

        Assert.Equal("[[Feature]] assigned to [[Developer]]", escaped);
    }
}