using SmartNotes.Whisper;

public class WhisperSmokeTest
{
    public static void ProvaCarrega()
    {
        var modelPath = @"C:\Users\polmi\Desktop\Dev\SmartNotes\SmartNotes.Whisper\models\ggml-medium-q5_0.bin";

        var service = new WhisperService(modelPath);

        Console.WriteLine("Model carregat correctament (via shim).");
    }
}