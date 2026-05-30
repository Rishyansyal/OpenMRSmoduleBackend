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
            logger.LogDebug("Herinnering ({window}) voor encounter {id} al verstuurd — overgeslagen.",
                cmd.ReminderWindow, cmd.EncounterId);
            return;
        }

        await scheduledReminderRepository.MarkSendingAsync(cmd.ScheduledReminderId, ct);

        var patient = await openMrsService.GetPatientAsync(cmd.OrganizationId, cmd.PatientId, ct);
        if (patient is null)
        {
            logger.LogWarning("Patiënt niet gevonden voor encounter {enc}.",
                cmd.EncounterId);
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
            logger.LogInformation("Patiënt voor encounter {enc} heeft geen contactgegevens — herinnering overgeslagen.",
                cmd.EncounterId);
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
        var content = BuildMessage(cmd, template?.Body);

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
            logger.LogInformation("Herinnering ({window}) verstuurd voor encounter {enc}.",
                cmd.ReminderWindow, cmd.EncounterId);
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

    private static string BuildMessage(SendReminderCommand cmd, string? templateBody)
    {
        var timeStr = cmd.EncounterStart.ToLocalTime().ToString(
            "dddd d MMMM 'om' HH:mm",
            new System.Globalization.CultureInfo("nl-NL"));
        var serviceType = cmd.ServiceType ?? "afspraak";
        var location = string.IsNullOrWhiteSpace(cmd.Location) ? "locatie onbekend" : cmd.Location!;
        var instructions = string.IsNullOrWhiteSpace(cmd.Instructions) ? "" : cmd.Instructions!;

        var body = templateBody ?? (cmd.ReminderWindow == "24h"
            ? "Herinnering: u heeft morgen een {type} op {tijd} bij {locatie}.{instructies} Neem contact op bij vragen."
            : "Herinnering: u heeft over ongeveer 1 uur een {type} op {tijd} bij {locatie}.{instructies}");

        var instructionsBlock = string.IsNullOrEmpty(instructions) ? "" : $" Belangrijk: {instructions}.";

        return body
            .Replace("{type}", serviceType)
            .Replace("{tijd}", timeStr)
            .Replace("{locatie}", location)
            .Replace("{instructies}", instructionsBlock);
    }
}
