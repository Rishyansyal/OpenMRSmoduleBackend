namespace Application.Webhooks;

public interface IOpenMrsWebhookSignatureValidator
{
    Task<WebhookSignatureValidationResult> ValidateAsync(
        string? organizationId,
        string? timestampHeader,
        string? signatureHeader,
        string body,
        CancellationToken ct = default);
}
