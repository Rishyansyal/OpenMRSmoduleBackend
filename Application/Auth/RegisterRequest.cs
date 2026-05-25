using System.ComponentModel.DataAnnotations;

namespace Application.Auth;

public record RegisterRequest(
    [Required][EmailAddress][MaxLength(256)] string Email,
    [Required][MinLength(8)][MaxLength(128)] string Password
);
