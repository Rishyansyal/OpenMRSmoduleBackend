using Application.Messaging;
using Application.OpenMrs;
using Application.Reminders;
using Domain;
using Infrastructure.Reminders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RemindersController(
    ReminderWorker reminderWorker,
    IReminderLogRepository reminderLogRepository,
    IOpenMrsService openMrsService,
    IMessagingService messagingService,
    IOptions<ReminderOptions> reminderOptions) : ControllerBase
{
    /// <summary>Handmatig een reminder-run triggeren — handig voor testen.</summary>
    [HttpPost("trigger")]
    public async Task<IActionResult> Trigger(CancellationToken ct)
    {
        await reminderWorker.ProcessAsync(ct);
        return Ok(new { message = "Reminder-run voltooid." });
    }

    /// <summary>
    /// Stuurt een demo-herinnering synchroon zodat het direct zichtbaar is in de logs.
    /// Bedoeld voor presentaties waarbij geen toekomstige afspraken in OpenMRS staan.
    /// </summary>
    [HttpPost("trigger/demo")]
    public async Task<IActionResult> TriggerDemo(CancellationToken ct)
    {
        var encounters = await openMrsService.GetUpcomingAppointmentsAsync(ct);
        var encounter = encounters.FirstOrDefault();
        if (encounter is null)
            return NotFound(new { message = "Geen encounters gevonden in OpenMRS." });

        var patient = await openMrsService.GetPatientAsync(encounter.PatientId, ct);
        if (patient is null)
            return NotFound(new { message = "Patiënt niet gevonden in OpenMRS." });

        var recipient = patient.Phone ?? patient.Email ?? "demo@example.com";
        var provider = reminderOptions.Value.DefaultProvider;
        var content = $"Demo: u heeft een afspraak op {encounter.Start:dddd d MMMM 'om' HH:mm}.";

        var result = await messagingService.SendAsync(
            provider,
            new SendMessageRequest([recipient], content, patient.Phone is not null ? "SMS" : "EMAIL"),
            ct);

        await reminderLogRepository.LogAsync(new ReminderLog
        {
            EncounterId = encounter.Id,
            ReminderWindow = "demo",
            Provider = provider,
            Success = result.Success,
            ErrorCode = result.Success ? null : "SEND_ERROR",
            EncounterStart = encounter.Start.ToUniversalTime(),
            PatientName = patient.DisplayName
        }, ct);

        return Ok(new
        {
            message = $"Demo-herinnering verstuurd voor {patient.DisplayName} via {provider}.",
            encounterId = encounter.Id,
            patientName = patient.DisplayName,
            provider
        });
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory([FromQuery] int count = 50, CancellationToken ct = default)
    {
        var logs = await reminderLogRepository.GetRecentAsync(count, ct);
        return Ok(logs);
    }
}
