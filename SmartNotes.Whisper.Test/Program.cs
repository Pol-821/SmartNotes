using SmartNotes.Whisper;

var model = @"C:\Users\polmi\Desktop\Dev\SmartNotes\SmartNotes.Whisper\models\ggml-medium-q5_0.bin";
var wav = @"C:\Users\polmi\Downloads\11-03-2026-17.32-16k.wav";

var service = new WhisperService(model);

Console.WriteLine("Carregant àudio...");
float[] audio = WavLoader.LoadWav(wav);

Console.WriteLine($"Àudio carregat: {audio.Length} mostres");

Console.WriteLine("Transcrivint...");

string text = service.TranscriureLong(audio, 10);

Console.WriteLine("Transcripció:");
Console.WriteLine(text);