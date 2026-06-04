using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[Route("[controller]")]
public class HealthController(ApplicationDbContext db) : ControllerBase
{
    /// <summary>Publiek health-check endpoint voor load balancers en orchestrators.</summary>
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Get() => Ok(new { status = "Healthy" });

    /// <summary>
    /// DB-bereikbaarheidscheck — vereist authenticatie om infrastructuurinfo te beschermen.
    /// </summary>
    [Authorize]
    [HttpGet("db")]
    public async Task<IActionResult> GetDb(CancellationToken ct)
    {
        var reachable = await db.Database.CanConnectAsync(ct);
        return Ok(new { status = "Healthy", db = reachable ? "reachable" : "unreachable" });
    }
}

