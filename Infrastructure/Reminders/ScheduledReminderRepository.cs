using Application.Reminders;
using Application.Security;
using Domain;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Reminders;

public class ScheduledReminderRepository(
    ApplicationDbContext db,
    IFieldEncryptionService encryption) : IScheduledReminderRepository
{
    public async Task<IReadOnlyList<ScheduledReminderDispatch>> ClaimDueAsync(
        DateTime nowUtc,
        int maxCount,
        CancellationToken ct = default)
    {
        var staleQueuedCutoff = nowUtc.AddMinutes(-15);
        var reminders = await db.ScheduledReminders
            .Include(r => r.AppointmentNotification)
            .Where(r =>
                r.ScheduledForUtc <= nowUtc &&
                (r.Status == ScheduledReminderStatus.Pending ||
                 (r.Status == ScheduledReminderStatus.Queued && r.UpdatedAtUtc < staleQueuedCutoff)) &&
                r.AppointmentNotification != null &&
                !r.AppointmentNotification.IsCancelled)
            .OrderBy(r => r.ScheduledForUtc)
            .Take(maxCount)
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        foreach (var reminder in reminders)
        {
            reminder.Status = ScheduledReminderStatus.Queued;
            reminder.UpdatedAtUtc = now;
        }

        await db.SaveChangesAsync(ct);

        return reminders.Select(r =>
        {
            var appointment = r.AppointmentNotification!;
            return new ScheduledReminderDispatch(
                r.Id,
                r.OrganizationId,
                r.EncounterId,
                encryption.Decrypt(appointment.PatientIdEncrypted),
                r.ReminderWindow,
                appointment.StartUtc,
                encryption.DecryptNullable(appointment.ServiceTypeEncrypted),
                r.Provider,
                encryption.DecryptNullable(appointment.LocationEncrypted),
                encryption.DecryptNullable(appointment.InstructionsEncrypted));
        }).ToList();
    }

    public async Task MarkSentAsync(Guid scheduledReminderId, CancellationToken ct = default)
    {
        var reminder = await db.ScheduledReminders.SingleOrDefaultAsync(r => r.Id == scheduledReminderId, ct);
        if (reminder is null) return;

        reminder.Status = ScheduledReminderStatus.Sent;
        reminder.SentAtUtc = DateTime.UtcNow;
        reminder.LastErrorCode = null;
        reminder.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task MarkFailedAsync(Guid scheduledReminderId, string errorCode, CancellationToken ct = default)
    {
        var reminder = await db.ScheduledReminders.SingleOrDefaultAsync(r => r.Id == scheduledReminderId, ct);
        if (reminder is null) return;

        reminder.Status = ScheduledReminderStatus.Failed;
        reminder.LastErrorCode = errorCode;
        reminder.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ScheduledReminderOverview>> GetRecentAsync(
        int count = 50,
        CancellationToken ct = default) =>
        await db.ScheduledReminders
            .Include(r => r.AppointmentNotification)
            .OrderByDescending(r => r.ScheduledForUtc)
            .Take(count)
            .Select(r => new ScheduledReminderOverview(
                r.Id,
                r.OrganizationId,
                r.EncounterId,
                r.ReminderWindow,
                r.ScheduledForUtc,
                r.AppointmentNotification == null ? DateTime.MinValue : r.AppointmentNotification.StartUtc,
                r.Provider,
                r.Status,
                r.AppointmentNotification != null && r.AppointmentNotification.IsCancelled,
                r.LastErrorCode))
            .ToListAsync(ct);
}
