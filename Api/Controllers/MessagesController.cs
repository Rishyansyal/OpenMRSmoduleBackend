using System.Security.Claims;
using Application.Messaging;
using Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MessagesController(
    IMessagingService messagingService,
    IAsyncMessageProvider asyncFlow,
    IMessageLogRepository messageLogRepository) : ControllerBase
{
    [HttpGet("providers")]
    public IActionResult GetProviders() =>
        Ok(messagingService.GetAvailableProviders());

    [HttpPost]
    public async Task<IActionResult> Send([FromBody] SendMessageApiRequest request, CancellationToken ct)
    {
        try
        {
            var result = await messagingService.SendAsync(
                request.Provider,
                new SendMessageRequest(request.Recipients, request.Content, request.Type, request.Subject),
                ct);

            await messageLogRepository.LogAsync(new MessageLog
            {
                Provider = request.Provider,
                MessageType = request.Type,
                RecipientCount = request.Recipients.Length,
                FailedCount = result.FailedRecipients.Length,
                ProviderMessageId = result.MessageId,
                Success = result.Success,
                ErrorCode = result.Error is null ? null : "SEND_ERROR",
                SentByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown"
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
        var logs = await messageLogRepository.GetRecentAsync(count, ct);
        return Ok(logs);
    }
}

public record SendMessageApiRequest(
    string Provider,
    string[] Recipients,
    string Content,
    string Type,
    string? Subject = null);
