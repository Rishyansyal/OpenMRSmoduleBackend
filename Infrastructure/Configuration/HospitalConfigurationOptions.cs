namespace Infrastructure.Configuration;

public class HospitalConfigurationOptions
{
    public List<HospitalOrganizationOptions> Organizations { get; set; } = [];
}

public class HospitalOrganizationOptions
{
    public string OrganizationId { get; set; } = "";
    public string OpenMrsBaseUrl { get; set; } = "";
    public string OpenMrsUsername { get; set; } = "";
    public string OpenMrsPassword { get; set; } = "";
    public string WebhookSecret { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public string DefaultProvider { get; set; } = "swiftsend";
    public string TimeZoneId { get; set; } = "UTC";
    public bool PollerEnabled { get; set; }
    public int PollerIntervalMinutes { get; set; } = 5;
    public int PollerLookaheadHours { get; set; } = 48;
    public int MaxDeliveryAttempts { get; set; } = 288;
    public int RetryBaseDelaySeconds { get; set; } = 60;
    public int RetryMaxDelayMinutes { get; set; } = 60;
    public List<HospitalProviderOptions> Providers { get; set; } = [];
}

public class HospitalProviderOptions
{
    public string ProviderName { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public string BaseUrl { get; set; } = "";
    public string StudentGroup { get; set; } = "";
    public Dictionary<string, string> Credentials { get; set; } = [];
}

