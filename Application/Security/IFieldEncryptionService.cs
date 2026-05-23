namespace Application.Security;

public interface IFieldEncryptionService
{
    string Encrypt(string value);
    string? EncryptNullable(string? value);
    string Decrypt(string encryptedValue);
    string? DecryptNullable(string? encryptedValue);
}
