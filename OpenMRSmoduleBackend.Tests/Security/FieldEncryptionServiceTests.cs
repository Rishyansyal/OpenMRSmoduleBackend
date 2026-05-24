using Infrastructure.Security;
using Microsoft.Extensions.Configuration;

namespace OpenMRSmoduleBackend.Tests.Security;

public class FieldEncryptionServiceTests
{
    [Fact]
    public void Encrypt_ThenDecrypt_ReturnsOriginalValue()
    {
        var service = CreateService();

        var encrypted = service.Encrypt("patient-123");

        Assert.StartsWith("v1:", encrypted);
        Assert.NotEqual("patient-123", encrypted);
        Assert.Equal("patient-123", service.Decrypt(encrypted));
    }

    [Fact]
    public void Encrypt_UsesDifferentNoncePerCall()
    {
        var service = CreateService();

        var first = service.Encrypt("same-value");
        var second = service.Encrypt("same-value");

        Assert.NotEqual(first, second);
    }

    private static FieldEncryptionService CreateService()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:EncryptionKey"] = Convert.ToBase64String(Enumerable.Range(0, 32).Select(i => (byte)i).ToArray())
            })
            .Build();

        return new FieldEncryptionService(config);
    }
}
