using Application.Reminders;
using Domain;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Reminders;

public class ReminderLogRepository(ApplicationDbContext db) : IReminderLogRepository
{
    public async Task<bool> AlreadySentAsync(string encounterId, string window, CancellationToken ct = default) =>
        await db.ReminderLogs.AnyAsync(
            r => r.EncounterId == encounterId && r.ReminderWindow == window && r.Success, ct);

    public async Task LogAsync(ReminderLog log, CancellationToken ct = default)
    {
        db.ReminderLogs.Add(log);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IEnumerable<ReminderLog>> GetRecentAsync(int count = 50, CancellationToken ct = default) =>
        await db.ReminderLogs
            .OrderByDescending(r => r.SentAt)
            .Take(count)
            .ToListAsync(ct);
}
