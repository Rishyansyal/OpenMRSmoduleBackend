namespace Infrastructure.Auth;

public class AdminBootstrapOptions
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    public bool AllowPublicRegistration { get; set; }
    public bool SeedOnStartup { get; set; } = true;
}
