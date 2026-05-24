namespace Application.Webhooks;

public interface IOpenMrsWebhookSignatureValidator
{
    WebhookSignatureValidationResult Validate(
        string? timestampHeader,
        string? signatureHeader,
        string body);
}
