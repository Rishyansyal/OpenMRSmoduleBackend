namespace Infrastructure.OpenMrs;

public class OpenMrsPollerOptions
{
    public bool Enabled { get; set; } = false;
    public int IntervalMinutes { get; set; } = 5;
    public int LookaheadHours { get; set; } = 48;
    public string OrganizationId { get; set; } = "openmrs-local";
}
