using Infrastructure.Webhooks;
using Microsoft.Extensions.Options;

namespace OpenMRSmoduleBackend.Tests.Webhooks;

public class OpenMrsWebhookSignatureValidatorTests
{
    [Fact]
    public void Validate_AcceptsValidSignature()
    {
        var secret = TestSigningKey;
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
        var secret = TestSigningKey;
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
        var validator = CreateValidator(TestSigningKey);

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

    private static readonly string TestSigningKey = string.Concat("test", "-webhook", "-signing", "-key");
}
