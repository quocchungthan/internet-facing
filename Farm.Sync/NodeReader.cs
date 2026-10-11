using System.Diagnostics;
using System.Text;

namespace AffineSync;

public interface ISnapshotReader
{
    Task<Snapshot> ReadAsync(CancellationToken cancellationToken);
}

public sealed class NodeReader(string path) : ISnapshotReader
{
    public async Task<Snapshot> ReadAsync(CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo("node")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        start.ArgumentList.Add(Path.GetFullPath(path));
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start Node reader.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(2));
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            var output = await stdout;
            var diagnostics = await stderr;
            if (!string.IsNullOrWhiteSpace(diagnostics))
                Console.Error.WriteLine($"Reader stderr: {diagnostics.TrimEnd()}");
            if (process.ExitCode != 0)
                throw new InvalidOperationException($"Node reader exited with code {process.ExitCode}.");
            return Snapshot.ParseUnvalidated(output);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
            await stdout;
            var diagnostics = await stderr;
            if (!string.IsNullOrWhiteSpace(diagnostics))
                Console.Error.WriteLine($"Reader stderr: {diagnostics.TrimEnd()}");
            if (!cancellationToken.IsCancellationRequested)
                throw new TimeoutException("Node reader exceeded the two-minute timeout.");
            throw;
        }
    }
}
