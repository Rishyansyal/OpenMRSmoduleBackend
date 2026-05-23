using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Application.Messaging;
using Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MessagesController(
    IMessagingService messagingService,
    IAsyncMessageProvider asyncFlow,
    IMessageLogRepository messageLogRepository) : ControllerBase
{
    private const int MaxHistoryCount   = 100;

    [HttpGet("providers")]
    public IActionResult GetProviders() =>
        Ok(messagingService.GetAvailableProviders());

    [HttpPost]
    [EnableRateLimiting("MessagePolicy")]
    public async Task<IActionResult> Send([FromBody] SendMessageApiRequest request, CancellationToken ct)
    {
        if (request.Recipients.Length > MessagingLimits.MaxRecipientCount)
            return BadRequest(new { error = $"Maximum {MessagingLimits.MaxRecipientCount} ontvangers per verzoek." });

        try
        {
            var result = await messagingService.SendAsync(
                request.Provider,
                new SendMessageRequest(request.Recipients, request.Content, request.Type, request.Subject),
                ct);

            await messageLogRepository.LogAsync(new MessageLog
            {
                Provider         = request.Provider,
                MessageType      = request.Type,
                RecipientCount   = request.Recipients.Length,
                FailedCount      = result.FailedRecipients.Length,
                ProviderMessageId = result.MessageId,
                Success          = result.Success,
                ErrorCode        = result.Error is null ? null : "SEND_ERROR",
                SentByUserId     = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown"
            }, ct);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("status/{trackingId}")]
    public async Task<IActionResult> GetStatus(string trackingId, CancellationToken ct)
    {
        var result = await asyncFlow.GetStatusAsync(trackingId, ct);
        return Ok(result);
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory([FromQuery] int count = 50, CancellationToken ct = default)
    {
        // Begrens het aantal records om zware DB-queries te voorkomen (DoS-bescherming)
        count = Math.Clamp(count, 1, MaxHistoryCount);
        var logs = await messageLogRepository.GetRecentAsync(count, ct);
        return Ok(logs);
    }
}

/// <summary>Maximum aantal ontvangers per messaging-verzoek (DoS-bescherming).</summary>
internal static class MessagingLimits
{
    internal const int MaxRecipientCount = 50;
}

public record SendMessageApiRequest(
    [Required] string Provider,
    [Required][MinLength(1)][MaxLength(MessagingLimits.MaxRecipientCount)] string[] Recipients,
    [Required][MaxLength(10_000)] string Content,
    [Required] string Type,
    [MaxLength(200)] string? Subject = null);

