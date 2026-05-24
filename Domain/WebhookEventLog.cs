namespace Domain;

public class WebhookEventLog
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string EventId { get; set; } = "";
    public string EventType { get; set; } = "";
    public string OrganizationId { get; set; } = "";
    public string ResourceType { get; set; } = "Encounter";
    public string ResourceId { get; set; } = "";
    public string PayloadSha256 { get; set; } = "";
    public bool Duplicate { get; set; }
    public bool Processed { get; set; }
    public string? ErrorCode { get; set; }
    public DateTimeOffset EventTimestamp { get; set; }
    public DateTime ReceivedAtUtc { get; init; } = DateTime.UtcNow;
}
