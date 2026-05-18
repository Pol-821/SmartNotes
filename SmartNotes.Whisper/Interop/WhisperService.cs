using System;
using System.Text;

namespace SmartNotes.Whisper;

public class WhisperService
{
    private readonly string _modelPath;

    public WhisperService(string modelPath)
    {
        _modelPath = modelPath ?? throw new ArgumentNullException(nameof(modelPath));
    }

    public string Transcriure(float[] audio)
    {
        var sb = new StringBuilder(1024 * 1024); // 1 MB de buffer

        int r = WhisperNative.sn_whisper_transcribe(
            _modelPath,
            audio,
            audio.Length,
            sb,
            sb.Capacity);

        if (r != 0)
            throw new Exception($"Error en la transcripció (codi {r}).");

        return sb.ToString().Trim();
    }

    public string TranscriureLong(float[] audio, int chunkSeconds = 10)
    {
        // De moment, fem-ho simple: tot de cop
        return Transcriure(audio);
    }
}