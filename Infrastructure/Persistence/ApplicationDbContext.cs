using Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<IdentityUser>(options)
{
    public DbSet<MessageLog> MessageLogs => Set<MessageLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasKey(u => u.Id);
            e.Property(u => u.Id).HasColumnName("id");
            e.Property(u => u.Email).HasColumnName("email").IsRequired();
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.PasswordHash).HasColumnName("password_hash").IsRequired();
            e.Property(u => u.CreatedAt).HasColumnName("created_at");
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
