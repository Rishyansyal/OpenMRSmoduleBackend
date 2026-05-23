using Application.Security;
using Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Infrastructure.Persistence;

public class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options,
    IEncryptionService encryption)
    : IdentityDbContext<IdentityUser>(options)
{
    public DbSet<MessageLog> MessageLogs => Set<MessageLog>();
    public DbSet<ReminderLog> ReminderLogs => Set<ReminderLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        var encryptConverter = new ValueConverter<string, string>(
            v => encryption.Encrypt(v),
            v => encryption.Decrypt(v));

        builder.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasKey(u => u.Id);
            e.Property(u => u.Id).HasColumnName("id");
            e.Property(u => u.Email).HasColumnName("email").IsRequired().HasConversion(encryptConverter);
            e.Property(u => u.EmailHash).HasColumnName("email_hash").IsRequired();
            e.HasIndex(u => u.EmailHash).IsUnique();
            e.Property(u => u.PasswordHash).HasColumnName("password_hash").IsRequired();
            e.Property(u => u.CreatedAt).HasColumnName("created_at");
        });

        builder.Entity<ReminderLog>(e =>
        {
            e.ToTable("reminder_logs");
            e.HasKey(r => r.Id);
            e.Property(r => r.Id).HasColumnName("id");
            e.Property(r => r.EncounterId).HasColumnName("encounter_id").IsRequired().HasConversion(encryptConverter);
            e.Property(r => r.EncounterIdHash).HasColumnName("encounter_id_hash").IsRequired();
            e.Property(r => r.ReminderWindow).HasColumnName("reminder_window").IsRequired();
            e.Property(r => r.Provider).HasColumnName("provider").IsRequired();
            e.Property(r => r.Success).HasColumnName("success");
            e.Property(r => r.ErrorCode).HasColumnName("error_code");
            e.Property(r => r.EncounterStart).HasColumnName("encounter_start");
            e.Property(r => r.SentAt).HasColumnName("sent_at");
            e.HasIndex(r => new { r.EncounterIdHash, r.ReminderWindow });
        });

        builder.Entity<MessageLog>(e =>
        {
            e.ToTable("message_logs");
            e.HasKey(m => m.Id);
            e.Property(m => m.Id).HasColumnName("id");
            e.Property(m => m.Provider).HasColumnName("provider").IsRequired();
            e.Property(m => m.MessageType).HasColumnName("message_type").IsRequired();
            e.Property(m => m.RecipientCount).HasColumnName("recipient_count");
            e.Property(m => m.FailedCount).HasColumnName("failed_count");
            e.Property(m => m.ProviderMessageId).HasColumnName("provider_message_id");
            e.Property(m => m.Success).HasColumnName("success");
            e.Property(m => m.ErrorCode).HasColumnName("error_code");
            e.Property(m => m.SentAt).HasColumnName("sent_at");
            e.Property(m => m.SentByUserId).HasColumnName("sent_by_user_id").IsRequired();
            e.HasIndex(m => m.SentAt);
        });
    }
}
