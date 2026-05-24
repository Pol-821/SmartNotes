using System.Diagnostics;

namespace SmartNotes.Api.Services;

public class FfmpegResult
{
    public int ExitCode { get; set; }
    public string StandardOutput { get; set; } = "";
    public string StandardError { get; set; } = "";
}

public class FfmpegRunner
{
    private readonly ILogger<FfmpegRunner> _logger;

    public FfmpegRunner(ILogger<FfmpegRunner> logger)
    {
        _logger = logger;
    }

    public async Task<FfmpegResult> RunAsync(string fileName, string arguments, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync();
        await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync(ct);

        return new FfmpegResult
        {
            ExitCode = process.ExitCode,
            StandardOutput = await outputTask,
        };
    }

    public async Task RunWithErrorDataAsync(string fileName, string arguments, Action<string> onErrorData, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        process.ErrorDataReceived += (s, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                onErrorData(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(); } catch { }
            throw;
        }

        if (process.ExitCode != 0)
            throw new Exception($"{fileName} ha fallat. ExitCode: {process.ExitCode}");
    }
}
