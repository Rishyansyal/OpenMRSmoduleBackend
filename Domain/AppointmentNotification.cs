namespace Domain;

public class AppointmentNotification
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string OrganizationId { get; set; } = "";
    public string EncounterId { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime StartUtc { get; set; }
    public DateTime? EndUtc { get; set; }
    public bool IsCancelled { get; set; }
    public string PatientIdEncrypted { get; set; } = "";
    public string? PatientDisplayEncrypted { get; set; }
    public string? ServiceTypeEncrypted { get; set; }
    public string? LocationEncrypted { get; set; }
    public string? InstructionsEncrypted { get; set; }
    public string LastEventId { get; set; } = "";
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<ScheduledReminder> ScheduledReminders { get; set; } = [];
}
