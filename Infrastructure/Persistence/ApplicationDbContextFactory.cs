using Application.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Infrastructure.Persistence;

/// <summary>
/// Design-time factory voor EF Core migraties (<c>dotnet ef migrations add …</c>).
/// Leest de connection string uitsluitend uit de omgevingsvariabele
/// <c>ConnectionStrings__DefaultConnection</c> — geen hardcoded fallback om
/// onbedoelde verbinding met een lokale database te voorkomen.
/// </summary>
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? throw new InvalidOperationException(
                "Stel de env var 'ConnectionStrings__DefaultConnection' in vóór het uitvoeren van migraties. " +
                "Voorbeeld: $Env:ConnectionStrings__DefaultConnection='Host=localhost;Port=5432;Database=openmrs;Username=openmrs;Password=...'");

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new ApplicationDbContext(options, new NullEncryptionService());
    }
}

// Stub uitsluitend voor design-time EF Core tools (migrations).
// Encrypt/decrypt worden niet aangeroepen tijdens migraties.
internal sealed class NullEncryptionService : IEncryptionService
{
    public string Encrypt(string plaintext) => plaintext;
    public string Decrypt(string ciphertext) => ciphertext;
    public string Hash(string value) => value;
}
