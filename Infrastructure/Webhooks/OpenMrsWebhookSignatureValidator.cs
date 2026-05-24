using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Application.Webhooks;
using Microsoft.Extensions.Options;

namespace Infrastructure.Webhooks;

public class OpenMrsWebhookSignatureValidator(IOptions<OpenMrsWebhookOptions> options)
    : IOpenMrsWebhookSignatureValidator
{
    private readonly OpenMrsWebhookOptions _options = options.Value;

    public WebhookSignatureValidationResult Validate(
        string? timestampHeader,
        string? signatureHeader,
        string body)
    {
        if (string.IsNullOrWhiteSpace(_options.Secret))
        {
            return new WebhookSignatureValidationResult(
                false,
                null,
                "WEBHOOK_SECRET_NOT_CONFIGURED",
                "OpenMRS webhook secret is not configured.");
        }

        if (string.IsNullOrWhiteSpace(timestampHeader))
            return Invalid("MISSING_TIMESTAMP", "X-OpenMRS-Timestamp is required.");

        if (!DateTimeOffset.TryParse(
                timestampHeader,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var timestamp))
        {
            return Invalid("INVALID_TIMESTAMP", "X-OpenMRS-Timestamp is not a valid timestamp.");
        }

        var skew = TimeSpan.FromMinutes(Math.Max(1, _options.AllowedClockSkewMinutes));
        if (DateTimeOffset.UtcNow - timestamp > skew || timestamp - DateTimeOffset.UtcNow > skew)
            return Invalid("STALE_TIMESTAMP", "Webhook timestamp is outside the allowed clock skew.", timestamp);

        if (string.IsNullOrWhiteSpace(signatureHeader) ||
            !signatureHeader.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
        {
            return Invalid("MISSING_SIGNATURE", "X-OpenMRS-Signature is required.", timestamp);
        }

        var providedHex = signatureHeader["sha256=".Length..].Trim();
        var expectedHex = ComputeSignatureHex(timestampHeader, body, _options.Secret);

        if (!FixedTimeEqualsHex(providedHex, expectedHex))
            return Invalid("INVALID_SIGNATURE", "Webhook signature is invalid.", timestamp);

        return new WebhookSignatureValidationResult(true, timestamp, null, null);
    }

    public static string ComputeSignatureHex(string timestampHeader, string body, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var bytes = Encoding.UTF8.GetBytes($"{timestampHeader}.{body}");
        return Convert.ToHexString(hmac.ComputeHash(bytes)).ToLowerInvariant();
    }

    private static bool FixedTimeEqualsHex(string providedHex, string expectedHex)
    {
        try
        {
            var provided = Convert.FromHexString(providedHex);
            var expected = Convert.FromHexString(expectedHex);
            return provided.Length == expected.Length &&
                   CryptographicOperations.FixedTimeEquals(provided, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static WebhookSignatureValidationResult Invalid(
        string code,
        string message,
        DateTimeOffset? timestamp = null) =>
        new(false, timestamp, code, message);
}
