using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Infrastructure.Persistence;

namespace Api.Controllers;

[ApiController]
[Route("[controller]")]
public class HealthController(IDbConnectionFactory connectionFactory) : ControllerBase
{
    /// <summary>Publiek health-check endpoint voor load balancers en orchestrators.</summary>
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Get() => Ok(new { status = "ok" });

    /// <summary>
    /// DB-bereikbaarheidscheck — vereist authenticatie om infrastructuurinfo te beschermen.
    /// </summary>
    [Authorize]
    [HttpGet("db")]
    public async Task<IActionResult> GetDb(CancellationToken ct)
    {
        using var connection = await connectionFactory.CreateConnectionAsync(ct);
        var result = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition("SELECT 1", cancellationToken: ct));
        return Ok(new { status = "ok", db = result == 1 ? "reachable" : "unreachable" });
    }
}

