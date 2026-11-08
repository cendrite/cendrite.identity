using Identity.Domain.Aggregates;
using Identity.Domain.Constants;
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

        builder.Property(u => u.AccountPublicKey)
            .HasColumnName("account_public_key")
            .IsRequired()
            .HasMaxLength(64);

        builder.HasIndex(u => u.AccountPublicKey)
            .HasDatabaseName("idx_account_public_key")
            .IsUnique();

        builder.Property(u => u.UserTag)
            .HasColumnName("user_tag")
            .IsRequired()
            .HasMaxLength(UserConstants.TagLength);

        builder.HasIndex(u => u.UserTag)
            .HasDatabaseName("idx_user_tag")
            .IsUnique();

        builder.HasMany(u => u.Devices)
            .WithOne()
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.AccountKeyRotations)
            .WithOne()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        base.Configure(builder);
    }
}