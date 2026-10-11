namespace AffineSync;

public sealed record WorkerConfiguration(string ConnectionString, string StatePath, string ReaderPath, int IntervalSeconds)
{
    public static WorkerConfiguration FromEnvironment()
    {
        var connectionString = Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("DATABASE_CONNECTION_STRING is required.");
        var interval = Environment.GetEnvironmentVariable("SYNC_INTERVAL_SECONDS") ?? "60";
        if (!int.TryParse(interval, out var seconds) || seconds <= 0)
            throw new InvalidOperationException("SYNC_INTERVAL_SECONDS must be a positive integer.");
        return new(connectionString,
            Environment.GetEnvironmentVariable("SYNC_STATE_PATH") ?? "/state/checkpoint.json",
            Environment.GetEnvironmentVariable("AFFINE_READER_PATH") ?? "reader/export.mjs", seconds);
    }
}
