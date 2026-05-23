using Infrastructure.Webhooks;
using Microsoft.Extensions.Options;

namespace OpenMRSmoduleBackend.Tests.Webhooks;

public class OpenMrsWebhookSignatureValidatorTests
{
    [Fact]
    public void Validate_AcceptsValidSignature()
    {
        const string secret = "test-webhook-secret";
        const string body = """{"encounterId":"enc-1"}""";
        var timestamp = DateTimeOffset.UtcNow.ToString("O");
        var signature = "sha256=" + OpenMrsWebhookSignatureValidator.ComputeSignatureHex(timestamp, body, secret);
        var validator = CreateValidator(secret);

        var result = validator.Validate(timestamp, signature, body);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_RejectsReplayOutsideClockSkew()
    {
        const string secret = "test-webhook-secret";
        const string body = """{"encounterId":"enc-1"}""";
        var timestamp = DateTimeOffset.UtcNow.AddMinutes(-30).ToString("O");
        var signature = "sha256=" + OpenMrsWebhookSignatureValidator.ComputeSignatureHex(timestamp, body, secret);
        var validator = CreateValidator(secret);

        var result = validator.Validate(timestamp, signature, body);

        Assert.False(result.IsValid);
        Assert.Equal("STALE_TIMESTAMP", result.ErrorCode);
    }

    [Fact]
    public void Validate_RejectsInvalidSignature()
    {
        var validator = CreateValidator("test-webhook-secret");

        var result = validator.Validate(DateTimeOffset.UtcNow.ToString("O"), "sha256=deadbeef", "{}");

        Assert.False(result.IsValid);
        Assert.Equal("INVALID_SIGNATURE", result.ErrorCode);
    }

    private static OpenMrsWebhookSignatureValidator CreateValidator(string secret) =>
        new(Options.Create(new OpenMrsWebhookOptions
        {
            Secret = secret,
            AllowedClockSkewMinutes = 5
        }));
}
