using System.Diagnostics;
using BackblazeMonitor.Core;

namespace BackblazeMonitor;

/// <summary>Reads the QoS policy through a hidden PowerShell, off the UI thread.</summary>
internal static class QosReader
{
    private const int TimeoutMs = 20_000;

    /// <summary>
    /// Current throttle in bits/s (0 = no policy), or <c>null</c> when it could not be read (the caller
    /// keeps what it shows). Blocking: call it from a worker thread.
    /// </summary>
    public static double? Read()
    {
        try
        {
            var psi = new ProcessStartInfo(AppPaths.PowerShellExe)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                Arguments = "-NoProfile -NonInteractive -EncodedCommand " + EncodedCommand.Encode(QosReadResult.GetScript()),
            };
            using var p = Process.Start(psi);
            if (p is null) return null;
            var err = p.StandardError.ReadToEndAsync();
            var outTask = p.StandardOutput.ReadToEndAsync();
            if (!p.WaitForExit(TimeoutMs))
            {
                try
                {
                    p.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Already gone
                }

                p.WaitForExit();
                return null;
            }

            p.WaitForExit();
            _ = err.Wait(1000);
            if (!outTask.Wait(1000)) return null;
            return p.ExitCode == 0 ? QosReadResult.Parse(outTask.Result) : null;
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>Switches Backblaze to automatic mode through bzcli (no admin rights needed).</summary>
internal static class BzCli
{
    private const int TimeoutMs = 60_000;

    /// <summary>Whether bzcli.exe is installed.</summary>
    public static bool Exists() => File.Exists(AppPaths.BzCli);

    /// <summary>
    /// Runs <c>bzcli configure</c> with a temporary configuration file, pumping messages while it runs.
    /// Returns whether it exited with 0. Always cleans up the temporary files.
    /// </summary>
    public static bool EnableAuto()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "bzauto_" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(tmp, BzAutoCommands.Json, System.Text.Encoding.ASCII);
            var psi = new ProcessStartInfo(AppPaths.BzCli)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                Arguments = BzAutoCommands.GetArguments(tmp),
            };
            using var p = Process.Start(psi);
            if (p is null) return false;
            // Drain both pipes so a chatty tool cannot block on a full buffer
            p.OutputDataReceived += (_, _) => { };
            p.ErrorDataReceived += (_, _) => { };
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            if (!UiWait.Until(() => p.HasExited, TimeoutMs))
            {
                try
                {
                    p.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Already gone
                }

                p.WaitForExit();
                return false;
            }

            p.WaitForExit();
            return p.ExitCode == 0;
        }
        catch
        {
            return false;
        }
        finally
        {
            try
            {
                File.Delete(tmp);
            }
            catch
            {
                // Temp file left behind: harmless
            }
        }
    }
}
