using System.Security.Cryptography;
using System.Text;
using Application.Security;

namespace Infrastructure.Security;

public class FieldEncryptionService(IConfiguration configuration) : IFieldEncryptionService
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    public string Encrypt(string value)
    {
        var plaintext = Encoding.UTF8.GetBytes(value);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(GetKey(), TagSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        var combined = new byte[NonceSize + TagSize + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, combined, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, combined, NonceSize, TagSize);
        Buffer.BlockCopy(ciphertext, 0, combined, NonceSize + TagSize, ciphertext.Length);
        return "v1:" + Convert.ToBase64String(combined);
    }

    public string? EncryptNullable(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : Encrypt(value);

    public string Decrypt(string encryptedValue)
    {
        if (!encryptedValue.StartsWith("v1:", StringComparison.Ordinal))
            throw new InvalidOperationException("Unsupported encrypted field version.");

        var combined = Convert.FromBase64String(encryptedValue[3..]);
        if (combined.Length < NonceSize + TagSize)
            throw new InvalidOperationException("Encrypted field is malformed.");

        var nonce = combined[..NonceSize];
        var tag = combined[NonceSize..(NonceSize + TagSize)];
        var ciphertext = combined[(NonceSize + TagSize)..];
        var plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(GetKey(), TagSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);
        return Encoding.UTF8.GetString(plaintext);
    }

    public string? DecryptNullable(string? encryptedValue) =>
        string.IsNullOrWhiteSpace(encryptedValue) ? null : Decrypt(encryptedValue);

    private byte[] GetKey()
    {
        var configured = configuration["Security:EncryptionKey"];
        if (string.IsNullOrWhiteSpace(configured))
            throw new InvalidOperationException(
                "Security:EncryptionKey is not configured. Set SECURITY_ENCRYPTION_KEY to a base64 encoded 32-byte key.");

        try
        {
            var key = Convert.FromBase64String(configured);
            if (key.Length == 32) return key;
        }
        catch (FormatException)
        {
            // Fall through to raw string support for local development.
        }

        var raw = Encoding.UTF8.GetBytes(configured);
        if (raw.Length == 32) return raw;

        throw new InvalidOperationException(
            "Security:EncryptionKey must decode to exactly 32 bytes for AES-256.");
    }
}
