using System.ComponentModel.DataAnnotations;

namespace Application.Auth;

public record LoginRequest(
    [Required][EmailAddress] string Email,
    [Required] string Password
);
