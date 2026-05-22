using Domain;

namespace Application.Auth;

public interface IUserRepository
{
    Task<User?> FindByEmailAsync(string email, CancellationToken ct = default);
    Task CreateAsync(User user, CancellationToken ct = default);
}
