using System.ComponentModel.DataAnnotations;

namespace Application.Auth;

public record RegisterRequest(
    [Required][EmailAddress][MaxLength(256)] string Email,
    [Required][MinLength(12)][MaxLength(128)] string Password
);
