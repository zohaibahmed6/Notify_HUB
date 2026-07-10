using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NotifyHub.Domain.Entities;

namespace NotifyHub.Infrastructure.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        builder.HasKey(rt => rt.Id);

        builder.Property(rt => rt.TokenHash)
            .HasMaxLength(64)
            .IsRequired();
        builder.HasIndex(rt => rt.TokenHash).IsUnique();

        builder.Property(rt => rt.CreatedAt).IsRequired();
        builder.Property(rt => rt.ExpiresAt).IsRequired();

        builder.Ignore(rt => rt.IsActive);

        builder.HasIndex(rt => rt.UserId);
    }
}
