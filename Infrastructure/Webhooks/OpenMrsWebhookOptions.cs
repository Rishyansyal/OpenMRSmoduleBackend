namespace Infrastructure.Webhooks;

public class OpenMrsWebhookOptions
{
    public string Secret { get; set; } = "";
    public int AllowedClockSkewMinutes { get; set; } = 5;
}
