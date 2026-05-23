using Application.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MessagesController(IMessagingService messagingService, IAsyncMessageProvider asyncFlow) : ControllerBase
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
}

public record SendMessageApiRequest(
    string Provider,
    string[] Recipients,
    string Content,
    string Type,
    string? Subject = null);
