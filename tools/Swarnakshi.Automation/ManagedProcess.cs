using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace Swarnakshi.Automation;

/// <summary>
/// Child-process and port plumbing shared by the API and client servers: start one process (never a
/// shell wrapper), wait for it to answer, and kill the whole tree afterwards.
/// </summary>
public static class ManagedProcess
{
    /// <summary>
    /// Starts a process with stdout/stderr redirected, so a server that fails to boot reports why
    /// instead of leaving the run waiting on a URL nothing will ever serve.
    /// </summary>
    public static Process Start(
        string fileName,
        string workingDirectory,
        IEnumerable<string> arguments,
        IDictionary<string, string>? environment = null,
        Action<string>? log = null)
    {
        var info = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var arg in arguments) info.ArgumentList.Add(arg);
        if (environment is not null)
            foreach (var (key, value) in environment) info.Environment[key] = value;

        var process = Process.Start(info)
            ?? throw new InvalidOperationException($"Could not start '{fileName}'.");

        // Drained on background threads. Without this the pipes fill and the child blocks writing to
        // a buffer nobody is reading — which looks exactly like a server that hung during startup.
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) log?.Invoke(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) log?.Invoke(e.Data); };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        return process;
    }

    /// <summary>True when something answers an HTTP request at the URL, whatever the status code.</summary>
    public static async Task<bool> IsRespondingAsync(string url, CancellationToken ct = default)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        try
        {
            using var response = await http.GetAsync(url, ct);
            return true;   // a 404 still proves something is listening and speaking HTTP
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Polls until the URL answers or the timeout expires, failing with the process's own output
    /// when it has already exited — the exit reason is far more useful than "timed out".
    /// </summary>
    public static async Task WaitUntilRespondingAsync(
        string url,
        Process? process,
        TimeSpan timeout,
        string what,
        CancellationToken ct = default)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;

        while (DateTimeOffset.UtcNow < deadline)
        {
            if (process is { HasExited: true })
                throw new InvalidOperationException(
                    $"{what} exited with code {process.ExitCode} before it started serving {url}.");

            if (await IsRespondingAsync(url, ct)) return;

            await Task.Delay(500, ct);
        }

        throw new TimeoutException($"{what} did not start serving {url} within {timeout.TotalSeconds:0}s.");
    }

    /// <summary>
    /// Fails with a usable sentence when a port is taken. Called before starting a server so a clash
    /// reads as a clash, rather than as an unexplained startup timeout later.
    /// </summary>
    public static void EnsurePortAvailable(int port, string what)
    {
        try
        {
            using var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
        }
        catch (SocketException)
        {
            throw new InvalidOperationException(
                $"Port {port} is already in use, so the UAT run cannot start its own {what}. " +
                "Stop whatever is on that port, or point the suite elsewhere with " +
                "SWARNAKSHI_UAT_BASE_URL / SWARNAKSHI_UAT_API_BASE_URL.");
        }
    }

    /// <summary>Kills the process and everything it started. Best effort — a dead process is fine.</summary>
    public static async Task StopAsync(Process? process)
    {
        if (process is null || process.HasExited) return;

        try
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
        }
        catch
        {
            // Already gone, or refusing to die — either way the run is over and the port will free.
        }
        finally
        {
            process.Dispose();
        }
    }
}
