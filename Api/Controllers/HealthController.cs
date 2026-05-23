using Dapper;
using Microsoft.AspNetCore.Mvc;
using Infrastructure.Persistence;

namespace Api.Controllers;

[ApiController]
[Route("[controller]")]
public class HealthController(IDbConnectionFactory connectionFactory) : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { status = "Healthy" });

    [HttpGet("db")]
    public async Task<IActionResult> GetDb(CancellationToken ct)
    {
        using var connection = await connectionFactory.CreateConnectionAsync(ct);
        var result = await connection.ExecuteScalarAsync<int>(new CommandDefinition("SELECT 1", cancellationToken: ct));
        return Ok(new { status = "Healthy", db = result == 1 ? "reachable" : "unreachable" });
    }
}
