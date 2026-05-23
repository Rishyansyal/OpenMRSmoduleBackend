namespace Infrastructure.Messaging.Options;

public class LegacyLinkOptions
{
    public string BaseUrl { get; set; } = "http://localhost:1337";
    public string Username { get; set; } = "legacylink-user";
    public string Password { get; set; } = "legacylink-password";
}
