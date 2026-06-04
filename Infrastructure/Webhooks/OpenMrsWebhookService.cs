using System.Security.Cryptography;
using System.Text;
using Application.Organizations;
using Application.Security;
using Application.Webhooks;
using Domain;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Webhooks;

public class OpenMrsWebhookService(
    ApplicationDbContext db,
    IFieldEncryptionService encryption,
    IOrganizationConfigRepository organizationConfigs) : IOpenMrsWebhookService
{
    public async Task<WebhookProcessingResult> ProcessAppointmentAsync(
        string eventId,
        string eventType,
        string organizationId,
        DateTimeOffset eventTimestamp,
        OpenMrsAppointmentWebhookRequest payload,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(eventId))
            throw new ArgumentException("Event id is required.", nameof(eventId));
        if (string.IsNullOrWhiteSpace(eventType))
            throw new ArgumentException("Event type is required.", nameof(eventType));
        if (string.IsNullOrWhiteSpace(organizationId))
            throw new ArgumentException("Organization id is required.", nameof(organizationId));
        if (string.IsNullOrWhiteSpace(payload.EncounterId))
            throw new ArgumentException("Encounter id is required.", nameof(payload));
        if (string.IsNullOrWhiteSpace(payload.PatientId))
            throw new ArgumentException("Patient id is required.", nameof(payload));

        var existingEvent = await db.WebhookEventLogs
            .AsNoTracking()
            .AnyAsync(e => e.EventId == eventId, ct);

        if (existingEvent)
        {
            return new WebhookProcessingResult(
                eventId,
                Accepted: true,
                Duplicate: true,
                Message: "Event was already processed.");
        }

        var config = await organizationConfigs.GetByIdAsync(organizationId, ct);
        if (config is null)
            throw new InvalidOperationException("OpenMRS organization is not configured or is disabled.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var log = new WebhookEventLog
        {
            EventId = eventId,
            EventType = eventType,
            OrganizationId = organizationId,
            ResourceType = "Encounter",
            ResourceId = payload.EncounterId,
            PayloadSha256 = ComputePayloadHash(payload),
            EventTimestamp = eventTimestamp,
            Duplicate = false
        };

        db.WebhookEventLogs.Add(log);

        var appointment = await db.AppointmentNotifications
            .Include(a => a.ScheduledReminders)
            .SingleOrDefaultAsync(
                a => a.OrganizationId == organizationId && a.EncounterId == payload.EncounterId,
                ct);

        if (appointment is null)
        {
            appointment = new AppointmentNotification
            {
                OrganizationId = organizationId,
                EncounterId = payload.EncounterId
            };
            db.AppointmentNotifications.Add(appointment);
        }

        var now = DateTime.UtcNow;
        var startUtc = ToUtc(payload.Start);
        var status = payload.Status.Trim();
        var isCancelled = IsCancellationEvent(eventType, status);

        appointment.Status = status;
        appointment.StartUtc = startUtc;
        appointment.EndUtc = payload.End.HasValue ? ToUtc(payload.End.Value) : null;
        appointment.IsCancelled = isCancelled;
        appointment.PatientIdEncrypted = encryption.Encrypt(payload.PatientId);
        appointment.PatientDisplayEncrypted = encryption.EncryptNullable(payload.PatientDisplay);
        appointment.ServiceTypeEncrypted = encryption.EncryptNullable(payload.ServiceType);
        appointment.LocationEncrypted = encryption.EncryptNullable(payload.Location);
        appointment.InstructionsEncrypted = encryption.EncryptNullable(payload.Instructions);
        appointment.LastEventId = eventId;
        appointment.UpdatedAtUtc = now;

        RecomputeReminderSchedule(appointment, config, now);

        log.Processed = true;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new WebhookProcessingResult(
            eventId,
            Accepted: true,
            Duplicate: false,
            Message: "Appointment webhook accepted.");
    }

    private static void RecomputeReminderSchedule(
        AppointmentNotification appointment,
        OrganizationRuntimeConfig config,
        DateTime nowUtc)
    {
        foreach (var pending in appointment.ScheduledReminders
                     .Where(r => r.Status is ScheduledReminderStatus.Pending or ScheduledReminderStatus.Queued))
        {
            pending.Status = ScheduledReminderStatus.Cancelled;
            pending.UpdatedAtUtc = nowUtc;
        }

        if (appointment.IsCancelled || appointment.StartUtc <= nowUtc)
            return;

        AddReminderIfFuture(appointment, "24h", appointment.StartUtc.AddHours(-24), config, nowUtc);
        AddReminderIfFuture(appointment, "1h", appointment.StartUtc.AddHours(-1), config, nowUtc);
    }

    private static void AddReminderIfFuture(
        AppointmentNotification appointment,
        string window,
        DateTime scheduledForUtc,
        OrganizationRuntimeConfig config,
        DateTime nowUtc)
    {
        if (scheduledForUtc <= nowUtc)
            return;

        var existing = appointment.ScheduledReminders
            .FirstOrDefault(r => r.ReminderWindow == window && r.Status == ScheduledReminderStatus.Pending);

        if (existing is not null)
        {
            existing.ScheduledForUtc = scheduledForUtc;
            existing.Provider = config.DefaultProvider;
            existing.MaxAttempts = config.MaxDeliveryAttempts;
            existing.RetryBaseDelaySeconds = config.RetryBaseDelaySeconds;
            existing.RetryMaxDelayMinutes = config.RetryMaxDelayMinutes;
            existing.NextAttemptAtUtc = scheduledForUtc;
            existing.UpdatedAtUtc = nowUtc;
            return;
        }

        appointment.ScheduledReminders.Add(new ScheduledReminder
        {
            AppointmentNotificationId = appointment.Id,
            OrganizationId = appointment.OrganizationId,
            EncounterId = appointment.EncounterId,
            ReminderWindow = window,
            ScheduledForUtc = scheduledForUtc,
            Provider = config.DefaultProvider,
            Status = ScheduledReminderStatus.Pending,
            MaxAttempts = config.MaxDeliveryAttempts,
            RetryBaseDelaySeconds = config.RetryBaseDelaySeconds,
            RetryMaxDelayMinutes = config.RetryMaxDelayMinutes,
            NextAttemptAtUtc = scheduledForUtc,
            UpdatedAtUtc = nowUtc
        });
    }

    private static bool IsCancellationEvent(string eventType, string status)
    {
        var normalizedEvent = eventType.Trim().ToUpperInvariant();
        var normalizedStatus = status.Trim().ToLowerInvariant();

        return normalizedEvent.Contains("VOIDED", StringComparison.Ordinal) ||
               normalizedEvent.Contains("CANCEL", StringComparison.Ordinal) ||
               normalizedStatus is "cancelled" or "canceled" or "voided" or "entered-in-error";
    }

    private static DateTime ToUtc(DateTime value) =>
        value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
            : value.ToUniversalTime();

    private static string ComputePayloadHash(OpenMrsAppointmentWebhookRequest payload)
    {
        var canonical = string.Join("|",
            payload.EncounterId,
            payload.PatientId,
            payload.Start.ToUniversalTime().ToString("O"),
            payload.End?.ToUniversalTime().ToString("O") ?? "",
            payload.Status,
            payload.ServiceType ?? "",
            payload.Location ?? "",
            payload.Instructions ?? "");

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))
            .ToLowerInvariant();
    }
}
