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

        var patient = await openMrsService.GetPatientAsync(cmd.PatientId, ct);
        if (patient is null)
        {
            logger.LogWarning("Patiënt niet gevonden voor encounter {enc}.",
                cmd.EncounterId);
            await scheduledReminderRepository.MarkFailedAsync(
                cmd.ScheduledReminderId,
                "PATIENT_NOT_FOUND",
                ct);
            return;
        }

        var recipient = patient.Phone ?? patient.Email;
        if (recipient is null)
        {
            logger.LogInformation("Patiënt voor encounter {enc} heeft geen contactgegevens — herinnering overgeslagen.",
                cmd.EncounterId);
            await scheduledReminderRepository.MarkFailedAsync(
                cmd.ScheduledReminderId,
                "NO_CONTACT_DETAILS",
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
            await scheduledReminderRepository.MarkSentAsync(cmd.ScheduledReminderId, ct);
            logger.LogInformation("Herinnering ({window}) verstuurd voor encounter {enc}.",
                cmd.ReminderWindow, cmd.EncounterId);
            return;
        }

        await scheduledReminderRepository.MarkFailedAsync(
            cmd.ScheduledReminderId,
            "SEND_ERROR",
            ct);

        // Gooi een exception zodat MassTransit de retry-policy triggert
        throw new InvalidOperationException(
            $"Versturen mislukt voor encounter {cmd.EncounterId}: {result.Error}");
    }

    private static string BuildMessage(SendReminderCommand cmd, string? templateBody)
    {
        var timeStr = cmd.EncounterStart.ToLocalTime().ToString(
            "dddd d MMMM 'om' HH:mm",
            new System.Globalization.CultureInfo("nl-NL"));
        var serviceType = cmd.ServiceType ?? "afspraak";

        var body = templateBody ?? (cmd.ReminderWindow == "24h"
            ? "Herinnering: u heeft morgen een {type} op {tijd}. Neem contact op bij vragen."
            : "Herinnering: u heeft over ongeveer 1 uur een {type} op {tijd}.");

        return body.Replace("{type}", serviceType).Replace("{tijd}", timeStr);
    }
}
