namespace Application.Reminders;

public interface IReminderLogRepository
{
    Task<bool> AlreadySentAsync(string encounterId, string window, CancellationToken ct = default);
    Task LogAsync(Domain.ReminderLog log, CancellationToken ct = default);
    Task<IEnumerable<ReminderLogOverview>> GetRecentAsync(int count = 50, CancellationToken ct = default);
}
