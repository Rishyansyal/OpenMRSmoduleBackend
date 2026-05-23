using Application.Messaging.Commands;
using Application.OpenMrs;
using Application.Reminders;
using Infrastructure.Reminders;
using MassTransit;
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
    IBus bus,
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
    /// Stuurt een demo-herinnering voor een bestaande encounter, ongeacht het tijdstip.
    /// Bedoeld voor presentaties waarbij geen toekomstige afspraken in OpenMRS staan.
    /// </summary>
    [HttpPost("trigger/demo")]
    public async Task<IActionResult> TriggerDemo(CancellationToken ct)
    {
        var encounters = await openMrsService.GetUpcomingAppointmentsAsync(ct);
        var encounter = encounters.FirstOrDefault();

        if (encounter is null)
            return NotFound(new { message = "Geen encounters gevonden in OpenMRS." });

        await bus.Publish(new SendReminderCommand(
            encounter.Id,
            encounter.PatientId,
            "demo",
            DateTime.UtcNow.AddHours(24),
            encounter.ServiceType,
            reminderOptions.Value.DefaultProvider), ct);

        return Ok(new
        {
            message = $"Demo-herinnering verstuurd voor encounter {encounter.Id}.",
            encounterId = encounter.Id,
            patientId = encounter.PatientId,
            provider = reminderOptions.Value.DefaultProvider
        });
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory([FromQuery] int count = 50, CancellationToken ct = default)
    {
        var logs = await reminderLogRepository.GetRecentAsync(count, ct);
        return Ok(logs);
    }
}
