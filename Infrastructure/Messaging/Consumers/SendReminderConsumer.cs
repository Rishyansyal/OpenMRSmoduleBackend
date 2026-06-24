using System.Diagnostics;
using Application.Messaging;
using Application.Messaging.Commands;
using Application.OpenMrs;
using Application.Reminders;
using Domain;
using Infrastructure.Observability;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Messaging.Consumers;

public class SendReminderConsumer(
    IOpenMrsService openMrsService,
    IMessagingService messagingService,
    IReminderLogRepository reminderLogRepository,
    IScheduledReminderRepository scheduledReminderRepository,
    IMessageTemplateRepository messageTemplateRepository,
    IReminderMessageRenderer reminderMessageRenderer,
    MessagingMetrics metrics,
    ILogger<SendReminderConsumer> logger) : IConsumer<SendReminderCommand>
{
    public async Task Consume(ConsumeContext<SendReminderCommand> context)
    {
        var cmd = context.Message;
        var ct = context.CancellationToken;

        // Idempotentie: skip als al succesvol verstuurd (kan voorkomen bij retry)
        if (await reminderLogRepository.AlreadySentAsync(cmd.EncounterId, cmd.ReminderWindow, ct))
        {
            logger.LogDebug("Herinnering ({window}) voor reminder {id} al verstuurd — overgeslagen.",
                cmd.ReminderWindow, cmd.ScheduledReminderId);
            return;
        }

        await scheduledReminderRepository.MarkSendingAsync(cmd.ScheduledReminderId, ct);
        await scheduledReminderRepository.MarkConsumedAsync(cmd.ScheduledReminderId, ct);

        var patient = await openMrsService.GetPatientAsync(cmd.OrganizationId, cmd.PatientId, ct);
        if (patient is null)
        {
            logger.LogWarning("Patiënt niet gevonden voor reminder {id}.",
                cmd.ScheduledReminderId);
            await scheduledReminderRepository.RecordDeliveryAttemptAsync(
                cmd.ScheduledReminderId,
                success: false,
                retryable: false,
                providerMessageId: null,
                errorCode: "PATIENT_NOT_FOUND",
                ct);
            return;
        }

        var recipient = patient.Phone ?? patient.Email;
        if (recipient is null)
        {
            logger.LogInformation("Patiënt voor reminder {id} heeft geen contactgegevens — herinnering overgeslagen.",
                cmd.ScheduledReminderId);
            await scheduledReminderRepository.RecordDeliveryAttemptAsync(
                cmd.ScheduledReminderId,
                success: false,
                retryable: false,
                providerMessageId: null,
                errorCode: "NO_CONTACT_DETAILS",
                ct);
            return;
        }

        var type = patient.Phone is not null ? "SMS" : "EMAIL";
        var template = await messageTemplateRepository.GetByWindowAsync(cmd.ReminderWindow, ct);
        var content = reminderMessageRenderer.Render(
            new ReminderMessageContext(
                cmd.ReminderWindow,
                cmd.EncounterStart,
                cmd.ServiceType,
                cmd.Location,
                cmd.Instructions),
            template?.Body);

        var sw = Stopwatch.StartNew();
        var result = await messagingService.SendAsync(
            cmd.Provider,
            new SendMessageRequest([recipient], content, type),
            cmd.OrganizationId,
            ct);
        sw.Stop();

        metrics.RecordReminderSent(cmd.ReminderWindow, result.Success);
        metrics.RecordSendDuration(cmd.Provider, sw.Elapsed.TotalMilliseconds);

        await reminderLogRepository.LogAsync(new ReminderLog
        {
            EncounterId = cmd.EncounterId,
            ReminderWindow = cmd.ReminderWindow,
            Provider = cmd.Provider,
            Success = result.Success,
            ErrorCode = result.Success ? null : "SEND_ERROR",
            EncounterStart = cmd.EncounterStart
        }, ct);

        if (result.Success)
        {
            await scheduledReminderRepository.RecordDeliveryAttemptAsync(
                cmd.ScheduledReminderId,
                success: true,
                retryable: false,
                providerMessageId: result.MessageId,
                errorCode: null,
                ct);
            logger.LogInformation("Herinnering ({window}) verstuurd voor reminder {id}.",
                cmd.ReminderWindow, cmd.ScheduledReminderId);
            return;
        }

        var retryable = IsRetryableSendError(result.Error);
        await scheduledReminderRepository.RecordDeliveryAttemptAsync(
            cmd.ScheduledReminderId,
            success: false,
            retryable,
            providerMessageId: result.MessageId,
            errorCode: retryable ? "SEND_RETRYABLE" : "SEND_PERMANENT",
            ct);
    }

    private static bool IsRetryableSendError(string? error)
    {
        if (string.IsNullOrWhiteSpace(error))
            return true;

        var normalized = error.ToLowerInvariant();
        return normalized.Contains("timeout", StringComparison.Ordinal) ||
               normalized.Contains("temporar", StringComparison.Ordinal) ||
               normalized.Contains("connection", StringComparison.Ordinal) ||
               normalized.Contains("network", StringComparison.Ordinal) ||
               normalized.Contains("token", StringComparison.Ordinal) ||
               normalized.Contains("http 408", StringComparison.Ordinal) ||
               normalized.Contains("http 429", StringComparison.Ordinal) ||
               normalized.Contains("http 5", StringComparison.Ordinal);
    }

}
