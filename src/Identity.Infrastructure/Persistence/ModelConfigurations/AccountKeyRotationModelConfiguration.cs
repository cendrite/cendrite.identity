using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Persistence.ModelConfigurations;

public class AccountKeyRotationModelConfiguration : BaseModelConfiguration<AccountKeyRotation>
{
    public override void Configure(EntityTypeBuilder<AccountKeyRotation> builder)
    {
        builder.ToTable("account_key_rotation");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(r => r.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(r => r.DeviceId)
            .HasColumnName("device_id")
            .IsRequired();

        builder.Property(r => r.OldAccountPublicKey)
            .HasColumnName("old_account_public_key")
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(r => r.NewAccountPublicKey)
            .HasColumnName("new_account_public_key")
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(r => r.Proof)
            .HasColumnName("proof")
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(r => r.Approval)
            .HasColumnName("approval")
            .IsRequired()
            .HasMaxLength(128);

        builder.HasIndex(r => r.UserId)
            .HasDatabaseName("idx_account_key_rotation_user_id");

        base.Configure(builder);
    }
}
