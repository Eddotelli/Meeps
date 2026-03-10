using API.Models;
using Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace API.Infrastructure.Data.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Type)
            .IsRequired()
            .HasConversion<int>();

        builder.HasIndex(c => c.Type)
            .IsUnique();

        // Seed data
        builder.HasData(
            new Category { Id = 1, Type = CategoryType.Sports },
            new Category { Id = 2, Type = CategoryType.Music },
            new Category { Id = 3, Type = CategoryType.Gaming },
            new Category { Id = 4, Type = CategoryType.Food },
            new Category { Id = 5, Type = CategoryType.Arts },
            new Category { Id = 6, Type = CategoryType.Technology },
            new Category { Id = 7, Type = CategoryType.Outdoor },
            new Category { Id = 8, Type = CategoryType.Social },
            new Category { Id = 9, Type = CategoryType.Education },
            new Category { Id = 10, Type = CategoryType.Other }
        );
    }
}
