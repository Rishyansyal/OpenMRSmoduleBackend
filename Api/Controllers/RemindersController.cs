using Application.Reminders;
using Domain;
using Infrastructure.Reminders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RemindersController(
    ReminderWorker reminderWorker,
    IReminderLogRepository reminderLogRepository,
    IScheduledReminderRepository scheduledReminderRepository,
    IMessageTemplateRepository messageTemplateRepository) : ControllerBase
{
    private const int MaxHistoryCount = 100;

    /// <summary>Handmatig een reminder-run triggeren — handig voor testen.</summary>
    [HttpPost("trigger")]
    public async Task<IActionResult> Trigger(CancellationToken ct)
    {
        await reminderWorker.ProcessAsync(ct);
        return Ok(new { message = "Reminder-run voltooid." });
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory([FromQuery] int count = 50, CancellationToken ct = default)
    {
        count = Math.Clamp(count, 1, MaxHistoryCount);
        var logs = await reminderLogRepository.GetRecentAsync(count, ct);
        return Ok(logs);
    }

    [HttpGet("scheduled")]
    public async Task<IActionResult> GetScheduled([FromQuery] int count = 50, CancellationToken ct = default)
    {
        var scheduled = await scheduledReminderRepository.GetRecentAsync(count, ct);
        return Ok(scheduled);
    }

    [HttpGet("templates")]
    public async Task<IActionResult> GetTemplates(CancellationToken ct)
    {
        var templates = await messageTemplateRepository.GetAllAsync(ct);
        return Ok(templates);
    }

    [HttpPut("templates/{window}")]
    public async Task<IActionResult> UpdateTemplate(
        string window,
        [FromBody] UpdateTemplateRequest request,
        CancellationToken ct)
    {
        if (window != "24h" && window != "1h")
            return BadRequest(new { error = "Ongeldig venster. Gebruik '24h' of '1h'." });

        if (string.IsNullOrWhiteSpace(request.Body))
            return BadRequest(new { error = "Berichttekst mag niet leeg zijn." });

        await messageTemplateRepository.UpsertAsync(new MessageTemplate
        {
            Window = window,
            Body = request.Body.Trim(),
            UpdatedAtUtc = DateTime.UtcNow
        }, ct);

        return Ok(new { message = $"Sjabloon voor {window} bijgewerkt." });
    }
}

public record UpdateTemplateRequest(string Body);
