namespace Application.Security;

public interface IEncryptionService
{
    string Encrypt(string plaintext);
    string Decrypt(string ciphertext);
    string Hash(string value); // HMAC-SHA256 voor lookup
}
