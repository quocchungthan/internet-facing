namespace Shed.Pages.Shared;

public sealed class OsTextInputModel
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string Type { get; init; } = "text";
    public string AutoComplete { get; init; } = string.Empty;
    public string? Value { get; init; }
    public bool Required { get; init; } = true;
    public bool AutoFocus { get; init; }
}


