using Identity.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Persistence.ModelConfigurations;

public class UserModelConfiguration : BaseModelConfiguration<User>
{
    public override void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("user");

        builder.HasKey(u => u.Id);

        builder.Property(rt => rt.Id)
            .HasColumnName("id");

        builder.OwnsOne(u => u.Email, email =>
        {
            email.Property(e => e.Address)
                .HasColumnName("email")
                .IsRequired(false)
                .HasColumnType("citext") // Ensure case-insensitive to prevent duplicate emails
                .HasMaxLength(255);

            email.HasIndex(e => e.Address)
                .HasDatabaseName("idx_email")
                .IsUnique();
        });
        builder.OwnsOne(u => u.Password, pw =>
        {
            pw.Property(p => p.HashedPassword)
                .HasColumnName("password")
                .IsRequired(false)
                .HasMaxLength(255);
        });

        builder.Property(u => u.AccountPublicKey)
            .HasColumnName("account_public_key")
            .HasMaxLength(64);

        builder.HasIndex(u => u.AccountPublicKey)
            .HasDatabaseName("idx_account_public_key")
            .IsUnique();

        builder.Property(u => u.IsVerified)
            .HasColumnName("is_verified")
            .IsRequired();

        builder.HasMany(u => u.VerificationCodes)
            .WithOne()
            .HasForeignKey(vc => vc.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.Devices)
            .WithOne()
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        base.Configure(builder);
    }
}