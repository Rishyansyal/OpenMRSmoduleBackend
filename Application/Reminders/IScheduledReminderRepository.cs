namespace Application.Reminders;

public interface IScheduledReminderRepository
{
    Task<IReadOnlyList<ScheduledReminderDispatch>> ClaimDueAsync(
        DateTime nowUtc,
        int maxCount,
        CancellationToken ct = default);

    Task MarkSentAsync(Guid scheduledReminderId, CancellationToken ct = default);

    Task MarkFailedAsync(Guid scheduledReminderId, string errorCode, CancellationToken ct = default);

    Task RecordQueuePublishAsync(
        Guid scheduledReminderId,
        Guid queueMessageId,
        CancellationToken ct = default);

    Task RecordQueuePublishFailureAsync(
        Guid scheduledReminderId,
        string errorCode,
        CancellationToken ct = default);

    Task MarkSendingAsync(Guid scheduledReminderId, CancellationToken ct = default);

    Task MarkConsumedAsync(Guid scheduledReminderId, CancellationToken ct = default);

    Task RecordDeliveryAttemptAsync(
        Guid scheduledReminderId,
        bool success,
        bool retryable,
        string? providerMessageId,
        string? errorCode,
        CancellationToken ct = default);

    Task RetryNowAsync(Guid scheduledReminderId, CancellationToken ct = default);

    Task<IReadOnlyList<ScheduledReminderOverview>> GetRecentAsync(
        int count = 50,
        CancellationToken ct = default);
}
