using System.Globalization;
using Farm.Data;
using Microsoft.EntityFrameworkCore;

namespace AffineSync;

public static class DatabaseDisplay
{
    public static async Task WriteAsync(AuraFarming database, TextWriter output, CancellationToken cancellationToken)
    {
        var metadata = await database.WebsiteMetadata.AsNoTracking().OrderBy(row => row.Id).ToListAsync(cancellationToken);
        var bookmarks = await database.PlatformBookmarks.AsNoTracking().OrderBy(row => row.Id).ToListAsync(cancellationToken);
        WriteTable(output, "WebsiteMetadata",
            ["Id", "DeveloperName", "Title", "Description", "RepositoryUrl", "CreatedAtUtc", "UpdatedAtUtc"],
            metadata.Select(row => new object?[]
            {
                row.Id, row.DeveloperName, row.Title, row.Description, row.RepositoryUrl,
                row.CreatedAtUtc, row.UpdatedAtUtc
            }));
        output.WriteLine();
        WriteTable(output, "PlatformBookmarks",
            ["Id", "Platform", "Url", "PlatformId", "Description", "Color", "DisplayOrder", "IsActive", "CreatedAtUtc"],
            bookmarks.Select(row => new object?[]
            {
                row.Id, row.Platform, row.Url, row.PlatformId, row.Description, row.Color,
                row.DisplayOrder, row.IsActive, row.CreatedAtUtc
            }));
    }

    public static void WriteTable(TextWriter output, string title, string[] headers, IEnumerable<object?[]> rows)
    {
        var values = rows.Select(row =>
        {
            if (row.Length != headers.Length)
                throw new ArgumentException("Row column count must match table headers.", nameof(rows));
            return row.Select(Format).ToArray();
        }).ToList();
        var widths = headers.Select((header, index) =>
            Math.Max(header.Length, values.Select(row => row[index].Length).DefaultIfEmpty(0).Max())).ToArray();
        var border = "+" + string.Join("+", widths.Select(width => new string('-', width + 2))) + "+";
        void WriteRow(string[] cells) =>
            output.WriteLine("| " + string.Join(" | ", cells.Select((cell, index) => cell.PadRight(widths[index]))) + " |");
        output.WriteLine(title);
        output.WriteLine(border);
        WriteRow(headers);
        output.WriteLine(border);
        foreach (var row in values) WriteRow(row);
        output.WriteLine(border);
        output.WriteLine($"{values.Count} row(s)");
    }

    private static string Format(object? value)
    {
        var text = value switch
        {
            null => "",
            DateTime date => date.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture),
            bool boolean => boolean ? "true" : "false",
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""
        };
        return string.Concat(text.Select(character => char.IsControl(character) ? ' ' : character));
    }
}
