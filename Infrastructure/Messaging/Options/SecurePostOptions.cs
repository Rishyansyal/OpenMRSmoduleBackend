namespace Infrastructure.Messaging.Options;

public class SecurePostOptions
{
    public string BaseUrl { get; set; } = "http://localhost:1337";
    public string ClientId { get; set; } = "securepost-client-id";
    public string ClientSecret { get; set; } = "securepost-secret-key";
}
