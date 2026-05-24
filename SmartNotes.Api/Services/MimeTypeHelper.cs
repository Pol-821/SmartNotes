namespace SmartNotes.Api.Services;

public static class MimeTypeHelper
{
    private static readonly Dictionary<string, string> ExtensionToMime = new(StringComparer.OrdinalIgnoreCase)
    {
        [".wav"] = "audio/wav",
        [".mp3"] = "audio/mpeg",
        [".m4a"] = "audio/mp4",
        [".flac"] = "audio/flac",
        [".ogg"] = "audio/ogg",
        [".aac"] = "audio/aac",
        [".wma"] = "audio/x-ms-wma",
        [".mp4"] = "audio/mp4",
        [".webm"] = "audio/webm",
    };

    private static readonly HashSet<string> AllowedExtensions = new(ExtensionToMime.Keys, StringComparer.OrdinalIgnoreCase);

    public static string? GetMimeType(string fileName)
    {
        var ext = Path.GetExtension(fileName);
        return ExtensionToMime.GetValueOrDefault(ext);
    }

    public static string GetMimeTypeOrDefault(string fileName, string fallback = "application/octet-stream")
    {
        return GetMimeType(fileName) ?? fallback;
    }

    public static bool IsAllowed(string fileName)
    {
        var ext = Path.GetExtension(fileName);
        return AllowedExtensions.Contains(ext);
    }

    public static readonly string[] AllowedExtensionsArray = AllowedExtensions.ToArray();
}
