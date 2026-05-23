using Application.Messaging;
using Application.Messaging.Commands;
using Application.OpenMrs;
using Application.Reminders;
using Domain;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Messaging.Consumers;

public class SendReminderConsumer(
    IOpenMrsService openMrsService,
    IMessagingService messagingService,
    IReminderLogRepository reminderLogRepository,
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
            logger.LogWarning("Patiënt {id} niet gevonden voor encounter {enc}.",
                cmd.PatientId, cmd.EncounterId);
            return;
        }

        var recipient = patient.Phone ?? patient.Email;
        if (recipient is null)
        {
            logger.LogInformation("Patiënt {name} heeft geen contactgegevens — herinnering overgeslagen.",
                patient.DisplayName);
            return;
        }

        var type = patient.Phone is not null ? "SMS" : "EMAIL";
        var content = BuildMessage(cmd);

        var result = await messagingService.SendAsync(
            cmd.Provider,
            new SendMessageRequest([recipient], content, type),
            ct);

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
            logger.LogInformation("Herinnering ({window}) verstuurd naar {name}.", cmd.ReminderWindow, patient.DisplayName);
        else
            // Gooi een exception zodat MassTransit de retry-policy triggert
            throw new InvalidOperationException(
                $"Versturen mislukt voor {patient.DisplayName}: {result.Error}");
    }

    private static string BuildMessage(SendReminderCommand cmd)
    {
        var timeStr = cmd.EncounterStart.ToLocalTime().ToString(
            "dddd d MMMM 'om' HH:mm",
            new System.Globalization.CultureInfo("nl-NL"));
        var type = cmd.ServiceType ?? "afspraak";

        return cmd.ReminderWindow == "24h"
            ? $"Herinnering: u heeft morgen een {type} op {timeStr}. Neem contact op bij vragen."
            : $"Herinnering: u heeft over ongeveer 1 uur een {type} op {timeStr}.";
    }
}
