namespace Domain;

public class ScheduledReminder
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid AppointmentNotificationId { get; set; }
    public AppointmentNotification? AppointmentNotification { get; set; }
    public string OrganizationId { get; set; } = "";
    public string EncounterId { get; set; } = "";
    public string ReminderWindow { get; set; } = ""; // "24h" | "1h"
    public DateTime ScheduledForUtc { get; set; }
    public string Provider { get; set; } = "";
    public string Status { get; set; } = ScheduledReminderStatus.Pending;
    public string? LastErrorCode { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public static class ScheduledReminderStatus
{
    public const string Pending = "pending";
    public const string Queued = "queued";
    public const string Sent = "sent";
    public const string Cancelled = "cancelled";
    public const string Failed = "failed";
}
