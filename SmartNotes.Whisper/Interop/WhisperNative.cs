using System;
using System.Runtime.InteropServices;
using System.Text;

namespace SmartNotes.Whisper;

internal static class WhisperNative
{
    private const string DllName = "whisper.dll";

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    internal static extern int sn_whisper_transcribe(
        string modelPath,
        float[] samples,
        int n_samples,
        StringBuilder outText,
        int outTextMax);
}