using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NotifyHub.Domain.Entities;

namespace NotifyHub.Infrastructure.Persistence.Configurations;

public class BookmarkConfiguration : IEntityTypeConfiguration<Bookmark>
{
    public void Configure(EntityTypeBuilder<Bookmark> builder)
    {
        builder.ToTable("bookmarks");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Label).HasMaxLength(100).IsRequired();
        builder.Property(b => b.Description).HasMaxLength(300).IsRequired();
        builder.Property(b => b.InsertText).HasMaxLength(1000).IsRequired();
    }
}
