using System.Security.Cryptography;
using System.Text;
using Application.Security;
using Microsoft.Extensions.Options;

namespace Infrastructure.Security;

/// <summary>
/// AES-256-GCM encryptie- en HMAC-SHA256 hash-service.
///
/// Sleutelmanagement:
///   De master key (32 byte, base64) wordt via HKDF (RFC 5869) uitgesplitst in twee
///   onafhankelijke subsleutels:
///     • _encKey — gebruikt voor AES-256-GCM (vertrouwelijkheid + integriteit)
///     • _hmacKey — gebruikt voor HMAC-SHA256 (deterministisch zoeken)
///   Zo voorkomt key-separation dat een aanval op de HMAC-context invloed heeft op de
///   encryptiecontext en vice versa.
/// </summary>
public class AesEncryptionService : IEncryptionService
{
    private readonly byte[] _encKey;   // 32 bytes — AES-256-GCM
    private readonly byte[] _hmacKey;  // 32 bytes — HMAC-SHA256

    public AesEncryptionService(IOptions<EncryptionOptions> options)
    {
        var masterKey = Convert.FromBase64String(options.Value.Key);
        if (masterKey.Length != 32)
            throw new InvalidOperationException(
                "Encryption:Key moet exact 32 bytes (256 bits, base64-gecodeerd) zijn.");

        // HKDF key derivation — RFC 5869
        _encKey  = HKDF.DeriveKey(HashAlgorithmName.SHA256, masterKey, 32,
            info: Encoding.UTF8.GetBytes("aes-gcm-encryption-v1"));
        _hmacKey = HKDF.DeriveKey(HashAlgorithmName.SHA256, masterKey, 32,
            info: Encoding.UTF8.GetBytes("hmac-sha256-search-v1"));
    }

    public string Encrypt(string plaintext)
    {
        var nonce = new byte[AesGcm.NonceByteSizes.MaxSize];  // 12 bytes
        RandomNumberGenerator.Fill(nonce);

        var data       = Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = new byte[data.Length];
        var tag        = new byte[AesGcm.TagByteSizes.MaxSize]; // 16 bytes

        using var aes = new AesGcm(_encKey, AesGcm.TagByteSizes.MaxSize);
        aes.Encrypt(nonce, data, ciphertext, tag);

        // Indeling: [12 nonce][ciphertext][16 tag]
        var result = new byte[nonce.Length + ciphertext.Length + tag.Length];
        nonce.CopyTo(result, 0);
        ciphertext.CopyTo(result, nonce.Length);
        tag.CopyTo(result, nonce.Length + ciphertext.Length);

        return Convert.ToBase64String(result);
    }

    public string Decrypt(string ciphertext)
    {
        var data      = Convert.FromBase64String(ciphertext);
        var nonce     = data[..12];
        var tag       = data[^16..];
        var encrypted = data[12..^16];
        var plaintext = new byte[encrypted.Length];

        using var aes = new AesGcm(_encKey, AesGcm.TagByteSizes.MaxSize);
        aes.Decrypt(nonce, encrypted, tag, plaintext);

        return Encoding.UTF8.GetString(plaintext);
    }

    /// <summary>
    /// HMAC-SHA256 over <paramref name="value"/> met de afgeleid HMAC-sleutel.
    /// Deterministisch (zelfde invoer → zelfde uitvoer) — geschikt voor zoek-index.
    /// </summary>
    public string Hash(string value) =>
        Convert.ToBase64String(
            HMACSHA256.HashData(_hmacKey, Encoding.UTF8.GetBytes(value)));
}

