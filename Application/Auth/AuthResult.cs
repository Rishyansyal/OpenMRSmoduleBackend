namespace Application.Auth;

public record AuthResult(string Token, DateTime ExpiresAt);
