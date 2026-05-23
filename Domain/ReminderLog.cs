namespace Domain;

public class ReminderLog
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string EncounterId { get; set; } = "";
    public string EncounterIdHash { get; set; } = "";
    public string ReminderWindow { get; init; } = ""; // "24h" | "1h"
    public string Provider { get; init; } = "";
    public bool Success { get; init; }
    public string? ErrorCode { get; init; }
    public DateTime EncounterStart { get; init; }
    public DateTime SentAt { get; init; } = DateTime.UtcNow;
    public string? PatientName { get; set; }
}
