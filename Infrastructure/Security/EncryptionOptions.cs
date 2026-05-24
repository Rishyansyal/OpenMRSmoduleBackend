namespace Infrastructure.Security;

public class EncryptionOptions
{
    public string Key { get; set; } = ""; // Base64-encoded 32 bytes (256 bits)
}
