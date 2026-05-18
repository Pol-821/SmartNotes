namespace SmartNotes.Api.Models;

public class RaspberryDevice
{
    public int Id { get; set; }
    public string SerialNumber { get; set; } = default!;
    public int? UserId { get; set; }
    public User? User { get; set; }
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
}