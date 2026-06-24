using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Application.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Auth;

public class AuthService(
    UserManager<IdentityUser> userManager,
    IConfiguration configuration) : IAuthService
{
    public async Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var existing = await userManager.FindByEmailAsync(request.Email);
        if (existing is not null)
            throw new InvalidOperationException("Email is already in use.");

        var user = new IdentityUser
        {
            UserName = request.Email,
            Email = request.Email,
            EmailConfirmed = true
        };

        var created = await userManager.CreateAsync(user, request.Password);
        if (!created.Succeeded)
            throw new InvalidOperationException(
                string.Join("; ", created.Errors.Select(e => e.Description)));

        return await GenerateTokenAsync(user);
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
            throw new UnauthorizedAccessException("Invalid credentials.");

        if (await userManager.IsLockedOutAsync(user))
            throw new UnauthorizedAccessException("Account is temporarily locked.");

        var validPassword = await userManager.CheckPasswordAsync(user, request.Password);
        if (!validPassword)
        {
            await userManager.AccessFailedAsync(user);
            throw new UnauthorizedAccessException("Invalid credentials.");
        }

        await userManager.ResetAccessFailedCountAsync(user);
        return await GenerateTokenAsync(user);
    }

    private async Task<AuthResult> GenerateTokenAsync(IdentityUser user)
    {
        var secretKey = configuration["Jwt:SecretKey"];
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            secretKey = configuration["JWT_SECRET"];
        }

        if (string.IsNullOrWhiteSpace(secretKey))
        {
            throw new InvalidOperationException("Jwt:SecretKey or JWT_SECRET is not configured.");
        }

        if (secretKey.Length < 32)
            throw new InvalidOperationException(
                "Jwt:SecretKey must be at least 32 characters for HMAC-SHA256.");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(configuration.GetValue<int>("Jwt:ExpiresInMinutes", 60));

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, user.Email ?? "")
        };

        var roles = await userManager.GetRolesAsync(user);
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: creds);

        return new AuthResult(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}

