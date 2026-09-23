using System.Text.Json;
using Farm.Git;

namespace Farm.Sandbox.Chickens;

public sealed class ChickenOptions
{
    public required string ArtifactsPath { get; init; }
    public TimeSpan SchedulePeriod { get; init; } = TimeSpan.FromHours(1);
    public TimeSpan QuietPeriod { get; init; } = TimeSpan.FromHours(1);
    public TimeSpan LeaseDuration { get; init; } = TimeSpan.FromHours(1);
    public bool RunImmediately { get; init; } = true;
    public bool EnablePush { get; init; }

    public static ChickenOptions FromEnvironment() => new()
    {
        ArtifactsPath = Required("FARM_CHICKENS_ARTIFACTS_PATH"),
        SchedulePeriod = TimeSpan.FromSeconds(OptionalInt("FARM_CHICKENS_SCHEDULE_SECONDS", 3600)),
        QuietPeriod = TimeSpan.FromSeconds(OptionalInt("FARM_CHICKENS_QUIET_PERIOD_SECONDS", 3600)),
        LeaseDuration = TimeSpan.FromSeconds(OptionalInt("FARM_CHICKENS_LEASE_SECONDS", 3600)),
        RunImmediately = OptionalBool("FARM_CHICKENS_RUN_IMMEDIATELY", true),
        EnablePush = OptionalBool("FARM_CHICKENS_ENABLE_PUSH", false)
    };

    public static IReadOnlyList<GitValidationCommand> LoadValidationCommands()
    {
        var json = Environment.GetEnvironmentVariable("FARM_CHICKENS_VALIDATION_COMMANDS_JSON");
        return string.IsNullOrWhiteSpace(json)
            ? []
            : JsonSerializer.Deserialize<List<GitValidationCommand>>(json) ?? [];
    }

    public static IReadOnlyDictionary<string, string> LoadSafeProcessEnvironment()
    {
        var json = Environment.GetEnvironmentVariable("FARM_CHICKENS_SAFE_PROCESS_ENV_JSON");
        return string.IsNullOrWhiteSpace(json)
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    internal static string Required(string name) =>
        Environment.GetEnvironmentVariable(name)?.Trim() is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"Environment variable '{name}' is required.");

    private static int OptionalInt(string name, int fallback) =>
        int.TryParse(Environment.GetEnvironmentVariable(name), out var value) && value > 0 ? value : fallback;

    private static bool OptionalBool(string name, bool fallback) =>
        bool.TryParse(Environment.GetEnvironmentVariable(name), out var value) ? value : fallback;
}