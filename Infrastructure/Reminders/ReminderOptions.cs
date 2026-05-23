namespace Infrastructure.Reminders;

public class ReminderOptions
{
    public bool Enabled { get; set; } = true;
    public int IntervalMinutes { get; set; } = 5;
    public string DefaultProvider { get; set; } = "swiftsend";
}
