using Application.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Infrastructure.Auth;

public class AdminBootstrapSeeder(
    UserManager<IdentityUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IOptions<AdminBootstrapOptions> options,
    ILogger<AdminBootstrapSeeder> logger)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (!await roleManager.RoleExistsAsync(AuthPolicies.AdminRole))
            await roleManager.CreateAsync(new IdentityRole(AuthPolicies.AdminRole));

        var email = options.Value.Email;
        var password = options.Value.Password;
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Admin bootstrap skipped because Admin:Email/Admin:Password are not configured.");
            return;
        }

        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new IdentityUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true
            };

            var created = await userManager.CreateAsync(user, password);
            if (!created.Succeeded)
                throw new InvalidOperationException(
                    "Admin bootstrap failed: " +
                    string.Join("; ", created.Errors.Select(e => e.Description)));
        }

        if (!await userManager.IsInRoleAsync(user, AuthPolicies.AdminRole))
            await userManager.AddToRoleAsync(user, AuthPolicies.AdminRole);
    }
}

