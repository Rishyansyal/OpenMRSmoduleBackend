using Infrastructure.DataRetention;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/data-retention")]
[Authorize]
public class DataRetentionController(DataRetentionWorker worker) : ControllerBase
{
    /// <summary>Handmatig een cleanup-run starten — handig voor testen.</summary>
    [HttpPost("trigger")]
    public async Task<IActionResult> Trigger(CancellationToken ct)
    {
        var result = await worker.ProcessAsync(ct);
        return Ok(result);
    }
}
