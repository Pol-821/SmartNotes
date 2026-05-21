using System.Diagnostics;
using System.Globalization;
using SmartNotes.Api.Models;

namespace SmartNotes.Api.Services;

public class AudioPreprocessor
{
    public async Task<string> EnhanceAudioAsync(string inputPath, CancellationToken ct)
    {
        string outputPath = inputPath.Replace(Path.GetExtension(inputPath), "_enhanced.wav");
        
        string audioFilters = "afftdn=nf=-25,highpass=f=200,lowpass=f=3000,loudnorm,silenceremove=stop_periods=-1:stop_duration=10:stop_threshold=-35dB";

        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }

        var psi = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-nostdin -y -i \"{inputPath}\" -af \"{audioFilters}\" -ar 16000 -ac 1 \"{outputPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        process.ErrorDataReceived += (s, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                if (e.Data.Contains("time="))
                {
                    string temps = e.Data.Split("time=")[1].Split(" ")[0];
                    Console.Write($"\r[FFMPEG + DENOISE] Processat: {temps} d'àudio...  ");
                }
            }
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

        Console.WriteLine("\n[AUDIO] Neteja profunda completada.");

        if (process.ExitCode != 0)
        {
            throw new Exception($"Error en el filtratge. ExitCode: {process.ExitCode}");
        }

        return outputPath;
    }

    public async Task<string> EnhanceAudioWithProgressAsync(string inputPath, TranscriptionJob job, CancellationToken ct)
    {
        string outputPath = inputPath.Replace(Path.GetExtension(inputPath), "_enhanced.wav");
        
        string audioFilters = "afftdn=nf=-25,highpass=f=200,lowpass=f=3000,loudnorm,silenceremove=stop_periods=-1:stop_duration=10:stop_threshold=-35dB";

        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }

        var psi = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-nostdin -y -i \"{inputPath}\" -af \"{audioFilters}\" -ar 16000 -ac 1 \"{outputPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        process.ErrorDataReceived += (s, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data) && e.Data.Contains("time="))
            {
                string temps = e.Data.Split("time=")[1].Split(" ")[0];
                Console.Write($"\r[FFMPEG + DENOISE] Processat: {temps} d'àudio...  ");
                
                if (TimeSpan.TryParse(temps, CultureInfo.InvariantCulture, out var processed))
                {
                    var totalSeconds = processed.TotalSeconds;
                    var percentage = (int)Math.Min(95, 5 + (totalSeconds / 600) * 20);
                    job.ProgressMessage = $"Netejant àudio... {temps} / {job.AudioDuration ?? "???:??:???"}";
                    job.ProgressPercentage = Math.Max(job.ProgressPercentage, percentage);
                }
            }
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

        Console.WriteLine("\n[AUDIO] Neteja profunda completada.");

        if (process.ExitCode != 0)
        {
            throw new Exception($"Error en el filtratge. ExitCode: {process.ExitCode}");
        }

        return outputPath;
    }

    public async Task<string> ExtractLanguageSampleAsync(string inputPath, CancellationToken ct)
    {
        string samplePath = inputPath.Replace(Path.GetExtension(inputPath), "_sample.wav");
        
        if (File.Exists(samplePath)) File.Delete(samplePath);

        double duration = await GetAudioDurationSecondsAsync(inputPath, ct);
        double startTime = duration < 60 ? 0 : 60;
        double sampleDuration = Math.Min(30, duration);

        var psi = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-nostdin -y -ss {startTime} -t {sampleDuration} -i \"{inputPath}\" -c copy \"{samplePath}\"",
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        process.Start();
        
        var errorTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync(ct);
        await errorTask;

        return samplePath;
    }

    public async Task<double> GetAudioDurationSecondsAsync(string inputPath, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "ffprobe",
            Arguments = $"-v error -show_entries format=duration -of csv=p=0 \"{inputPath}\"",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var process = new Process { StartInfo = psi };
        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync(ct);

        if (double.TryParse(output.Trim(), CultureInfo.InvariantCulture, out var duration))
            return duration;

        return 0;
    }
}
