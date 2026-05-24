using Application.Reminders;
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
    IScheduledReminderRepository scheduledReminderRepository) : ControllerBase
{
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
        var logs = await reminderLogRepository.GetRecentAsync(count, ct);
        return Ok(logs);
    }

    [HttpGet("scheduled")]
    public async Task<IActionResult> GetScheduled([FromQuery] int count = 50, CancellationToken ct = default)
    {
        var scheduled = await scheduledReminderRepository.GetRecentAsync(count, ct);
        return Ok(scheduled);
    }
}
