namespace AffineSync;

// A heartbeat file next to the checkpoint, touched after every successful pass (even when nothing changed).
// `--healthcheck` fails when it is missing or stale, which lets Docker flag a worker stuck in failures.
public sealed class SyncHealth(string statePath)
{
    private readonly string path = Path.GetFullPath(statePath) + ".ok";

    public void MarkSuccess(DateTimeOffset now)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, now.ToString("O"));
    }

    public bool IsHealthy(DateTimeOffset now, TimeSpan maxAge) =>
        File.Exists(path) && DateTimeOffset.TryParse(File.ReadAllText(path), out var last) && now - last <= maxAge;

    public static TimeSpan Backoff(int intervalSeconds, int consecutiveFailures)
    {
        var seconds = intervalSeconds * Math.Pow(2, Math.Min(consecutiveFailures, 10));
        return TimeSpan.FromSeconds(Math.Min(seconds, Math.Max(intervalSeconds, 900)));
    }

    public static TimeSpan MaxAge(int intervalSeconds) => TimeSpan.FromSeconds(Math.Max(intervalSeconds, 900) * 2 + 60);
}
