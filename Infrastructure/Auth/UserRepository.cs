using Application.Auth;
using Dapper;
using Domain;
using Infrastructure.Persistence;

namespace Infrastructure.Auth;

public class UserRepository(IDbConnectionFactory connectionFactory) : IUserRepository
{
    public async Task<User?> FindByEmailAsync(string email, CancellationToken ct = default)
    {
        using var conn = await connectionFactory.CreateConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<User>(
            new CommandDefinition(
                "SELECT id, email, password_hash AS PasswordHash, created_at AS CreatedAt FROM users WHERE email = @email",
                new { email },
                cancellationToken: ct
            )
        );
    }

    public async Task CreateAsync(User user, CancellationToken ct = default)
    {
        using var conn = await connectionFactory.CreateConnectionAsync(ct);
        await conn.ExecuteAsync(
            new CommandDefinition(
                "INSERT INTO users (id, email, password_hash, created_at) VALUES (@Id, @Email, @PasswordHash, @CreatedAt)",
                user,
                cancellationToken: ct
            )
        );
    }
}
