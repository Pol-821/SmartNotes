using System.Globalization;
using SmartNotes.Api.Models;

namespace SmartNotes.Api.Services;

public class AudioPreprocessor
{
    private readonly FfmpegRunner _ffmpeg;
    private readonly ILogger<AudioPreprocessor> _logger;

    public AudioPreprocessor(FfmpegRunner ffmpeg, ILogger<AudioPreprocessor> logger)
    {
        _ffmpeg = ffmpeg;
        _logger = logger;
    }

    private const string AudioFilters = "afftdn=nf=-25,highpass=f=200,lowpass=f=3000,loudnorm,silenceremove=stop_periods=-1:stop_duration=10:stop_threshold=-35dB";

    public async Task<string> EnhanceAudioAsync(string inputPath, CancellationToken ct)
    {
        return await EnhanceAudioInternalAsync(inputPath, null, ct);
    }

    public async Task<string> EnhanceAudioWithProgressAsync(string inputPath, TranscriptionJob job, CancellationToken ct)
    {
        return await EnhanceAudioInternalAsync(inputPath, job, ct);
    }

    private async Task<string> EnhanceAudioInternalAsync(string inputPath, TranscriptionJob? job, CancellationToken ct)
    {
        var dir = Path.GetDirectoryName(inputPath) ?? ".";
        var nameWithoutExt = Path.GetFileNameWithoutExtension(inputPath);
        string outputPath = Path.Combine(dir, nameWithoutExt + "_enhanced.wav");

        if (File.Exists(outputPath))
            File.Delete(outputPath);

        await _ffmpeg.RunWithErrorDataAsync("ffmpeg",
            $"-nostdin -y -i \"{inputPath}\" -af \"{AudioFilters}\" -ar 16000 -ac 1 \"{outputPath}\"",
            data =>
            {
                if (data.Contains("time="))
                {
                    string temps = data.Split("time=")[1].Split(" ")[0];
                    _logger.LogInformation("FFMPEG processat: {Temps}", temps);
                    if (job != null)
                    {
                        job.ProgressMessage = $"Netejant àudio... {temps}";
                        job.ProgressPercentage = Math.Max(job.ProgressPercentage, 5 + GetPercentageFromTime(temps));
                    }
                }
            }, ct);

        _logger.LogInformation("Neteja d'àudio completada: {Output}", outputPath);
        return outputPath;
    }

    private static int GetPercentageFromTime(string timeStr)
    {
        if (TimeSpan.TryParse(timeStr, CultureInfo.InvariantCulture, out var processed))
            return (int)Math.Min(95, 5 + (processed.TotalSeconds / 600) * 20);
        return 5;
    }

    public async Task<string> ExtractLanguageSampleAsync(string inputPath, CancellationToken ct)
    {
        var dir = Path.GetDirectoryName(inputPath) ?? ".";
        var nameWithoutExt = Path.GetFileNameWithoutExtension(inputPath);
        string samplePath = Path.Combine(dir, nameWithoutExt + "_sample.wav");

        if (File.Exists(samplePath)) File.Delete(samplePath);

        double duration = await GetAudioDurationSecondsAsync(inputPath, ct);
        double startTime = duration < 60 ? 0 : 60;
        double sampleDuration = Math.Min(30, duration);

        var result = await _ffmpeg.RunAsync("ffmpeg",
            $"-nostdin -y -ss {startTime} -t {sampleDuration} -i \"{inputPath}\" -c copy \"{samplePath}\"", ct);

        if (result.ExitCode != 0)
            throw new Exception($"Error extraient mostra d'idioma. ExitCode: {result.ExitCode}");

        return samplePath;
    }

    public async Task<double> GetAudioDurationSecondsAsync(string inputPath, CancellationToken ct)
    {
        var result = await _ffmpeg.RunAsync("ffprobe",
            $"-v error -show_entries format=duration -of csv=p=0 \"{inputPath}\"", ct);

        if (double.TryParse(result.StandardOutput.Trim(), CultureInfo.InvariantCulture, out var duration))
            return duration;

        return 0;
    }
}
