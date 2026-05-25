using Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace Api.Controllers;

[ApiController]
[Route("[controller]")]
public class AuthController(IAuthService authService, ILogger<AuthController> logger) : ControllerBase
{
    [AllowAnonymous]
    [EnableRateLimiting("AuthPolicy")]
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct)
    {
        try
        {
            var result = await authService.RegisterAsync(request, ct);
            logger.LogInformation("Security Event: Successful registration for email {Email}", request.Email);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning("Security Event: Failed registration attempt for email {Email}. Reason: {Reason}", request.Email, ex.Message);
            // Geef GEEN interne foutmelding terug — dit voorkomt e-mail-enumeratie.
            // Een aanvaller mag niet weten of een e-mailadres al bestaat.
            return Conflict(new { error = "A user with the provided details already exists." });
        }
    }

    [AllowAnonymous]
    [EnableRateLimiting("AuthPolicy")]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        try
        {
            var result = await authService.LoginAsync(request, ct);
            logger.LogInformation("Security Event: Successful login for user {Email}", request.Email);
            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            logger.LogWarning("Security Event: Failed login attempt for user {Email}", request.Email);
            return Unauthorized(new { error = "Invalid credentials." });
        }
    }

    [Authorize]
    [HttpGet("me")]
    public IActionResult Me()
    {
        var id    = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = User.FindFirstValue(ClaimTypes.Email);
        return Ok(new { id, email });
    }
}

