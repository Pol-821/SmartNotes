using System;
using System.IO;
using System.Linq;

namespace SmartNotes.Whisper;

public static class WavLoader
{
    public static float[] LoadWav(string path)
    {
        using var fs = File.OpenRead(path);
        using var br = new BinaryReader(fs);

        // RIFF header
        var riff = new string(br.ReadChars(4));
        if (riff != "RIFF") throw new Exception("No és un WAV vàlid.");

        br.ReadInt32(); // chunk size

        var wave = new string(br.ReadChars(4));
        if (wave != "WAVE") throw new Exception("No és un WAV vàlid.");

        // Format chunk
        var fmt = new string(br.ReadChars(4));
        if (fmt != "fmt ") throw new Exception("Format WAV no suportat.");

        var fmtSize = br.ReadInt32();
        var audioFormat = br.ReadInt16();
        var numChannels = br.ReadInt16();
        var sampleRate = br.ReadInt32();
        br.ReadInt32(); // byte rate
        br.ReadInt16(); // block align
        var bitsPerSample = br.ReadInt16();

        if (audioFormat != 1)
            throw new Exception("Només es suporten WAV PCM.");

        if (bitsPerSample != 16)
            throw new Exception("Només es suporten WAV 16-bit PCM.");

        // Skip extra fmt bytes if any
        if (fmtSize > 16)
            br.ReadBytes(fmtSize - 16);

        // Data chunk
        var dataHeader = new string(br.ReadChars(4));
        while (dataHeader != "data")
        {
            var chunkSize = br.ReadInt32();
            br.ReadBytes(chunkSize);
            dataHeader = new string(br.ReadChars(4));
        }

        var dataSize = br.ReadInt32();
        var bytes = br.ReadBytes(dataSize);

        // Convert 16-bit PCM to float
        short[] samples = new short[bytes.Length / 2];
        Buffer.BlockCopy(bytes, 0, samples, 0, bytes.Length);

        float[] floatSamples = samples.Select(s => s / 32768f).ToArray();

        // Stereo → Mono
        if (numChannels == 2)
        {
            floatSamples = floatSamples
                .Where((x, i) => i % 2 == 0)
                .ToArray();
        }

        // Whisper requereix 16 kHz
        if (sampleRate != 16000)
            throw new Exception("Aquest WAV no és 16 kHz. Cal resamplejar-lo.");

        return floatSamples;
    }
}