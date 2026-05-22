using System.ComponentModel.DataAnnotations;

namespace Application.Auth;

public record RegisterRequest(
    [Required][EmailAddress] string Email,
    [Required][MinLength(8)] string Password
);
