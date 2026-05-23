namespace Domain;

public class OrganizationIntegrationConfig
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string OrganizationId { get; set; } = "";
    public string DefaultProvider { get; set; } = "swiftsend";
    public string TimeZoneId { get; set; } = "UTC";
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
