using System.Diagnostics;
using System.Text.Json;

namespace Farm.Mind.Notes;

public record DecodedNote(string Markdown, NoteSummaryDto[] Pages, string[] UnsupportedBlocks);

public class NoteDecoder(IHostEnvironment environment)
{
    public async Task<DecodedNote> DecodeAsync(IReadOnlyList<byte[]> updates, bool root, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("node")
            {
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
                UseShellExecute = false, CreateNoWindow = true
            }
        };
        process.StartInfo.ArgumentList.Add("--max-old-space-size=128");
        process.StartInfo.ArgumentList.Add(Path.Combine(environment.ContentRootPath, "Notes", "reader", "decode.mjs"));
        process.StartInfo.Environment.Clear();
        process.StartInfo.Environment["PATH"] = Environment.GetEnvironmentVariable("PATH");
        var started = false;
        try
        {
            started = process.Start();
            var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var error = process.StandardError.ReadToEndAsync(timeout.Token);
            await JsonSerializer.SerializeAsync(process.StandardInput.BaseStream, new
            {
                root,
                updates = updates.Select(Convert.ToBase64String).ToArray()
            }, cancellationToken: timeout.Token);
            process.StandardInput.Close();
            await process.WaitForExitAsync(timeout.Token);
            await error;
            if (process.ExitCode != 0) throw new NotePortalException("Note's stored document is incomplete, corrupt or unsupported by the database reader.");
            return JsonSerializer.Deserialize<DecodedNote>(await output, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new NotePortalException("Note decoder returned no document.");
        }
        catch (System.ComponentModel.Win32Exception) { throw new NotePortalException("The Note database reader requires Node.js on the Mind server."); }
        catch (JsonException) { throw new NotePortalException("Note decoder returned an invalid document."); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new NotePortalException("Note decoding exceeded the read time limit."); }
        finally
        {
            if (started && !process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
            }
        }
    }
}
