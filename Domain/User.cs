namespace Domain;

public class User
{
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Versleuteld e-mailadres (AES-256-GCM).</summary>
    public required string Email { get; init; }

    /// <summary>HMAC-SHA256 hash van het e-mailadres — voor deterministisch zoeken.</summary>
    public string EmailHash { get; init; } = "";

    /// <summary>BCrypt-hash van het wachtwoord — niet muteerbaar na aanmaken.</summary>
    public required string PasswordHash { get; init; }

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
