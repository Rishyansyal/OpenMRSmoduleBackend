namespace Domain;

public class User
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Email { get; init; }
    public required string PasswordHash { get; set; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
