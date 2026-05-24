namespace Application.Reminders;

public interface IScheduledReminderRepository
{
    Task<IReadOnlyList<ScheduledReminderDispatch>> ClaimDueAsync(
        DateTime nowUtc,
        int maxCount,
        CancellationToken ct = default);

    Task MarkSentAsync(Guid scheduledReminderId, CancellationToken ct = default);

    Task MarkFailedAsync(Guid scheduledReminderId, string errorCode, CancellationToken ct = default);

    Task<IReadOnlyList<ScheduledReminderOverview>> GetRecentAsync(
        int count = 50,
        CancellationToken ct = default);
}
