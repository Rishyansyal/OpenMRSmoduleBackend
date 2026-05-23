namespace Infrastructure.DataRetention;

public class DataRetentionOptions
{
    public bool Enabled { get; set; } = true;
    // Patiëntgerelateerde data (reminder_logs): max 14 dagen bewaren
    public int PatientDataRetentionDays { get; set; } = 14;
    // Meta-informatie (message_logs): max 1 jaar bewaren
    public int MessageLogRetentionDays { get; set; } = 365;
}
