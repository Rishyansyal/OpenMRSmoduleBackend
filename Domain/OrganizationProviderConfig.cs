namespace Domain;

public class OrganizationProviderConfig
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string OrganizationId { get; set; } = "";
    public string ProviderName { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public string BaseUrl { get; set; } = "";
    public string StudentGroup { get; set; } = "";
    public string CredentialsJsonEncrypted { get; set; } = "";
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

