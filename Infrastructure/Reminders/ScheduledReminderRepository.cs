using System.Data;
using Application.Reminders;
using Application.Security;
using Domain;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Reminders;

public class ScheduledReminderRepository(
    ApplicationDbContext db,
    IFieldEncryptionService fieldEncryption,
    IEncryptionService encryption) : IScheduledReminderRepository
{
    public async Task<IReadOnlyList<ScheduledReminderDispatch>> ClaimDueAsync(
        DateTime nowUtc,
        int maxCount,
        CancellationToken ct = default)
    {
        var staleQueuedCutoff = nowUtc.AddMinutes(-15);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var reminders = await db.ScheduledReminders
            .Include(r => r.AppointmentNotification)
            .Where(r =>
                r.ScheduledForUtc <= nowUtc &&
                (r.Status == ScheduledReminderStatus.Pending ||
                 (r.Status == ScheduledReminderStatus.RetryWait && r.NextAttemptAtUtc <= nowUtc) ||
                 (r.Status == ScheduledReminderStatus.Queued && r.UpdatedAtUtc < staleQueuedCutoff) ||
                 (r.Status == ScheduledReminderStatus.Sending && r.UpdatedAtUtc < staleQueuedCutoff)) &&
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
            reminder.QueueMessageId = null;
            reminder.QueuedAtUtc = null;
            reminder.ConsumedAtUtc = null;
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return reminders.Select(r =>
        {
            var appointment = r.AppointmentNotification!;
            return new ScheduledReminderDispatch(
                r.Id,
                r.OrganizationId,
                r.EncounterId,
                fieldEncryption.Decrypt(appointment.PatientIdEncrypted),
                r.ReminderWindow,
                appointment.StartUtc,
                fieldEncryption.DecryptNullable(appointment.ServiceTypeEncrypted),
                r.Provider,
                r.AttemptCount,
                r.MaxAttempts,
                fieldEncryption.DecryptNullable(appointment.LocationEncrypted),
                fieldEncryption.DecryptNullable(appointment.InstructionsEncrypted));
        }).ToList();
    }

    public async Task RecordQueuePublishAsync(
        Guid scheduledReminderId,
        Guid queueMessageId,
        CancellationToken ct = default)
    {
        var reminder = await db.ScheduledReminders.SingleOrDefaultAsync(r => r.Id == scheduledReminderId, ct);
        if (reminder is null) return;

        reminder.Status = ScheduledReminderStatus.Queued;
        reminder.QueueMessageId = queueMessageId.ToString("D");
        reminder.QueuedAtUtc = DateTime.UtcNow;
        reminder.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task RecordQueuePublishFailureAsync(
        Guid scheduledReminderId,
        string errorCode,
        CancellationToken ct = default)
    {
        var reminder = await db.ScheduledReminders.SingleOrDefaultAsync(r => r.Id == scheduledReminderId, ct);
        if (reminder is null) return;

        var now = DateTime.UtcNow;
        reminder.Status = ScheduledReminderStatus.RetryWait;
        reminder.LastErrorCode = errorCode;
        reminder.NextAttemptAtUtc = now.Add(ComputeBackoff(reminder));
        reminder.QueueMessageId = null;
        reminder.QueuedAtUtc = null;
        reminder.ConsumedAtUtc = null;
        reminder.UpdatedAtUtc = now;
        await db.SaveChangesAsync(ct);
    }

    public async Task MarkSendingAsync(Guid scheduledReminderId, CancellationToken ct = default)
    {
        var reminder = await db.ScheduledReminders.SingleOrDefaultAsync(r => r.Id == scheduledReminderId, ct);
        if (reminder is null) return;

        reminder.Status = ScheduledReminderStatus.Sending;
        reminder.LastAttemptAtUtc = DateTime.UtcNow;
        reminder.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task MarkConsumedAsync(Guid scheduledReminderId, CancellationToken ct = default)
    {
        var reminder = await db.ScheduledReminders.SingleOrDefaultAsync(r => r.Id == scheduledReminderId, ct);
        if (reminder is null) return;

        reminder.ConsumedAtUtc = DateTime.UtcNow;
        reminder.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task RecordDeliveryAttemptAsync(
        Guid scheduledReminderId,
        bool success,
        bool retryable,
        string? providerMessageId,
        string? errorCode,
        CancellationToken ct = default)
    {
        var reminder = await db.ScheduledReminders.SingleOrDefaultAsync(r => r.Id == scheduledReminderId, ct);
        if (reminder is null) return;

        var now = DateTime.UtcNow;
        reminder.AttemptCount++;
        reminder.LastAttemptAtUtc = now;
        reminder.ProviderMessageId = providerMessageId ?? reminder.ProviderMessageId;
        reminder.LastErrorCode = errorCode;
        reminder.UpdatedAtUtc = now;

        if (success)
        {
            reminder.Status = ScheduledReminderStatus.Sent;
            reminder.SentAtUtc = now;
            reminder.NextAttemptAtUtc = null;
        }
        else if (!retryable)
        {
            reminder.Status = ScheduledReminderStatus.FailedPermanent;
            reminder.NextAttemptAtUtc = null;
        }
        else if (reminder.AttemptCount >= reminder.MaxAttempts)
        {
            reminder.Status = ScheduledReminderStatus.DeadLettered;
            reminder.NextAttemptAtUtc = null;
        }
        else
        {
            reminder.Status = ScheduledReminderStatus.RetryWait;
            reminder.NextAttemptAtUtc = now.Add(ComputeBackoff(reminder));
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task RetryNowAsync(Guid scheduledReminderId, CancellationToken ct = default)
    {
        var reminder = await db.ScheduledReminders.SingleOrDefaultAsync(r => r.Id == scheduledReminderId, ct);
        if (reminder is null) return;

        reminder.Status = ScheduledReminderStatus.Pending;
        reminder.NextAttemptAtUtc = DateTime.UtcNow;
        reminder.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ScheduledReminderOverview>> GetRecentAsync(
        int count = 50,
        CancellationToken ct = default)
    {
        var reminders = await db.ScheduledReminders
            .Include(r => r.AppointmentNotification)
            .OrderByDescending(r => r.ScheduledForUtc)
            .Take(count)
            .ToListAsync(ct);

        return reminders
            .Select(r => new ScheduledReminderOverview(
                r.Id,
                r.OrganizationId,
                encryption.Hash(r.EncounterId),
                r.ReminderWindow,
                r.ScheduledForUtc,
                r.AppointmentNotification == null ? DateTime.MinValue : r.AppointmentNotification.StartUtc,
                r.Provider,
                r.Status,
                r.AppointmentNotification != null && r.AppointmentNotification.IsCancelled,
                r.LastErrorCode,
                r.AttemptCount,
                r.MaxAttempts,
                r.LastAttemptAtUtc,
                r.NextAttemptAtUtc,
                r.QueueMessageId,
                r.QueuedAtUtc,
                r.ConsumedAtUtc,
                r.ProviderMessageId))
            .ToList();
    }

    private static TimeSpan ComputeBackoff(ScheduledReminder reminder)
    {
        var exponentialSeconds = reminder.RetryBaseDelaySeconds *
                                 Math.Pow(2, Math.Max(0, reminder.AttemptCount - 1));
        var cappedSeconds = Math.Min(
            exponentialSeconds,
            TimeSpan.FromMinutes(reminder.RetryMaxDelayMinutes).TotalSeconds);
        return TimeSpan.FromSeconds(Math.Max(reminder.RetryBaseDelaySeconds, cappedSeconds));
    }
}
