using System.Diagnostics;
using Npgsql;

namespace AffineSync;

// `--check`: read-only report that tells you what is missing before (or after) the sync runs.
public static class SetupCheck
{
    private static readonly string[] RequiredTables = ["WebsiteMetadata", "PlatformBookmarks", "PortfolioProjects", "PortfolioExperiences"];

    public static async Task<int> RunAsync(string readerPath, bool showIds, CancellationToken cancellationToken)
    {
        var failures = await CheckFarmDatabaseAsync(cancellationToken);
        failures += await CheckAffineAsync(readerPath, showIds, cancellationToken);
        Console.WriteLine();
        Console.WriteLine(failures == 0 ? "Everything the sync needs is ready." : $"{failures} problem(s) found. Fix the FAIL lines above and run the check again.");
        return failures == 0 ? 0 : 1;
    }

    private static async Task<int> CheckFarmDatabaseAsync(CancellationToken cancellationToken)
    {
        var connectionString = Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
            return Fail("DATABASE_CONNECTION_STRING is not set", "add it to sync.env; the password is in /etc/farm/postgres.password on the VPS.");
        Console.WriteLine("PASS  DATABASE_CONNECTION_STRING is set");
        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            Console.WriteLine("PASS  Logged in to the Farm database");
            await using var command = connection.CreateCommand();
            command.CommandText = "select count(*) from information_schema.tables where table_name = any(@names)";
            command.Parameters.AddWithValue("names", RequiredTables);
            var found = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
            if (found == RequiredTables.Length)
            {
                Console.WriteLine("PASS  The Farm database has the AuraFarming and portfolio tables");
                return 0;
            }
            // The worker creates missing tables itself on start, so this is informational.
            Console.WriteLine($"INFO  The Farm database has {found} of {RequiredTables.Length} expected tables; the worker (or Farm) creates the rest on start");
            return 0;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Fail($"Could not log in to the Farm database ({exception.GetBaseException().GetType().Name})",
                "check Host (eldervibe-farm-postgres), Port, Database (aurafarming) and the password, which is the one in /etc/farm/postgres.password on the VPS. The container must be on the farm-network.");
        }
    }

    private static async Task<int> CheckAffineAsync(string readerPath, bool showIds, CancellationToken cancellationToken)
    {
        var checkPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(readerPath))!, "check.mjs");
        if (!File.Exists(checkPath))
            return Fail("The AFFiNE check script is missing from this image", "rebuild and redeploy the sync image.");
        var start = new ProcessStartInfo("node") { UseShellExecute = false };
        start.ArgumentList.Add(checkPath);
        if (showIds) start.ArgumentList.Add("--show-ids");
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start Node.");
        await process.WaitForExitAsync(cancellationToken);
        return process.ExitCode == 0 ? 0 : 1;
    }

    private static int Fail(string label, string fix)
    {
        Console.WriteLine($"FAIL  {label}");
        Console.WriteLine($"      -> {fix}");
        return 1;
    }
}
