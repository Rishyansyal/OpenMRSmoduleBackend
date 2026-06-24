namespace Domain;

public class OrganizationIntegrationConfig
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string OrganizationId { get; set; } = "";
    public string OpenMrsBaseUrl { get; set; } = "";
    public string OpenMrsUsernameEncrypted { get; set; } = "";
    public string OpenMrsPasswordEncrypted { get; set; } = "";
    public string WebhookSecretEncrypted { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public string DefaultProvider { get; set; } = "swiftsend";
    public string TimeZoneId { get; set; } = "UTC";
    public bool PollerEnabled { get; set; }
    public int PollerIntervalMinutes { get; set; } = 5;
    public int PollerLookaheadHours { get; set; } = 48;
    public int MaxDeliveryAttempts { get; set; } = 288;
    public int RetryBaseDelaySeconds { get; set; } = 60;
    public int RetryMaxDelayMinutes { get; set; } = 60;
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
