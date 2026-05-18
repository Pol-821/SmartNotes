using System.Text.Json.Serialization;
using System.Text.Json;

namespace SmartNotes.Api.Models;

public class SmartSummary
{
    [JsonPropertyName("Titol")] 
    public string Titol { get; set; } = string.Empty;
    public string ResumGeneral { get; set; } = string.Empty;
    public List<Subtema> Subtemes { get; set; } = new();
    public List<string> TasquesProfessor { get; set; } = new();
    public List<string> PropostaEstudi { get; set; } = new();

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtraData { get; set; }

    public class Subtema
    {
        public string Nom { get; set; } = string.Empty;
        public string Explicacio { get; set; } = string.Empty;
    }
}