using Application.Auth;
using Application.Security;
using Dapper;
using Domain;
using Infrastructure.Persistence;

namespace Infrastructure.Auth;

public class UserRepository(IDbConnectionFactory connectionFactory, IEncryptionService encryption) : IUserRepository
{
    public async Task<User?> FindByEmailAsync(string email, CancellationToken ct = default)
    {
        var hash = encryption.Hash(email);
        using var conn = await connectionFactory.CreateConnectionAsync(ct);
        var row = await conn.QuerySingleOrDefaultAsync<UserRow>(
            new CommandDefinition(
                "SELECT id, email, email_hash AS EmailHash, password_hash AS PasswordHash, created_at AS CreatedAt FROM users WHERE email_hash = @hash",
                new { hash },
                cancellationToken: ct));

        if (row is null) return null;

        return new User
        {
            Id = row.Id,
            Email = encryption.Decrypt(row.Email),
            EmailHash = row.EmailHash,
            PasswordHash = row.PasswordHash,
            CreatedAt = row.CreatedAt
        };
    }

    public async Task CreateAsync(User user, CancellationToken ct = default)
    {
        var encryptedEmail = encryption.Encrypt(user.Email);
        var emailHash = encryption.Hash(user.Email);

        using var conn = await connectionFactory.CreateConnectionAsync(ct);
        await conn.ExecuteAsync(
            new CommandDefinition(
                "INSERT INTO users (id, email, email_hash, password_hash, created_at) VALUES (@Id, @Email, @EmailHash, @PasswordHash, @CreatedAt)",
                new { user.Id, Email = encryptedEmail, EmailHash = emailHash, user.PasswordHash, user.CreatedAt },
                cancellationToken: ct));
    }

    private sealed record UserRow(Guid Id, string Email, string EmailHash, string PasswordHash, DateTime CreatedAt);
}
