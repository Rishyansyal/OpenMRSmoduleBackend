namespace Infrastructure.Messaging.Options;

public class SwiftSendOptions
{
    public string BaseUrl { get; set; } = "http://localhost:1337";
    public string ApiKey { get; set; } = "";
}
