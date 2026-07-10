using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NotifyHub.Domain.Entities;

namespace NotifyHub.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Username)
            .HasMaxLength(100)
            .IsRequired();
        builder.HasIndex(u => u.Username).IsUnique();

        builder.Property(u => u.PasswordHash)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(u => u.Role)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.HasMany(u => u.RefreshTokens)
            .WithOne(rt => rt.User)
            .HasForeignKey(rt => rt.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
