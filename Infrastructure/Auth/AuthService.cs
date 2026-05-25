using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Application.Auth;
using Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Auth;

public class AuthService(IUserRepository userRepository, IConfiguration configuration) : IAuthService
{
    // BCrypt work factor 12 → ~250ms per hash (goed evenwicht tussen veiligheid en snelheid)
    private const int BcryptWorkFactor = 12;

    public async Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var existing = await userRepository.FindByEmailAsync(request.Email, ct);
        if (existing is not null)
            throw new InvalidOperationException("Email is already in use.");

        var user = new User
        {
            Email        = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, BcryptWorkFactor)
        };

        await userRepository.CreateAsync(user, ct);
        return GenerateToken(user);
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await userRepository.FindByEmailAsync(request.Email, ct);
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid credentials.");

        return GenerateToken(user);
    }

    private AuthResult GenerateToken(User user)
    {
        var secretKey = configuration["Jwt:SecretKey"]
            ?? throw new InvalidOperationException("Jwt:SecretKey is not configured.");

        // Minimale sleutellengte: 32 tekens voor HMAC-SHA256 (256 bits)
        if (secretKey.Length < 32)
            throw new InvalidOperationException(
                "Jwt:SecretKey moet minimaal 32 tekens bevatten (256 bits voor HMAC-SHA256).");

        var key     = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var creds   = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var now     = DateTime.UtcNow;
        var expires = now.AddMinutes(configuration.GetValue<int>("Jwt:ExpiresInMinutes", 60));

        var token = new JwtSecurityToken(
            issuer:    configuration["Jwt:Issuer"],
            audience:  configuration["Jwt:Audience"],
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email)
            ],
            notBefore: now,      // Token is niet geldig vóór uitgifte (beschermt tegen klok-drift)
            expires:   expires,
            signingCredentials: creds
        );

        return new AuthResult(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}

