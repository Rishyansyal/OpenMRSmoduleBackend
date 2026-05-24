namespace Application.DataRetention;

public interface IDataRetentionService
{
    Task<DataRetentionResult> RunAsync(CancellationToken ct = default);
}

public record DataRetentionResult(
    int ReminderLogsDeleted,
    int MessageLogsDeleted,
    int AppointmentNotificationsDeleted,
    int WebhookEventLogsDeleted);
