using Application.Reminders;
using Application.Auth;
using Domain;
using Infrastructure.Reminders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = AuthPolicies.AdminOnly)]
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

    [HttpGet("dead-lettered")]
    public async Task<IActionResult> GetDeadLettered([FromQuery] int count = 50, CancellationToken ct = default)
    {
        var scheduled = await scheduledReminderRepository.GetRecentAsync(count, ct);
        return Ok(scheduled.Where(r => r.Status == ScheduledReminderStatus.DeadLettered));
    }

    [HttpGet("templates")]
    public async Task<IActionResult> GetTemplates(CancellationToken ct)
    {
        var templates = await messageTemplateRepository.GetAllAsync(ct);
        return Ok(templates);
    }

}
