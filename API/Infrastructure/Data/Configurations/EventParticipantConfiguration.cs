using API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace API.Infrastructure.Data.Configurations;

public class EventParticipantConfiguration : IEntityTypeConfiguration<EventParticipant>
{
    public void Configure(EntityTypeBuilder<EventParticipant> builder)
    {
        builder.HasKey(ep => new { ep.EventId, ep.UserId });

        builder.Property(ep => ep.JoinedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Property(ep => ep.Role)
            .IsRequired();

        builder.Property(ep => ep.BlockedReason)
            .HasMaxLength(500)
            .IsRequired(false);

        builder.Property(ep => ep.BlockedAt)
            .IsRequired(false);

        builder.HasOne(ep => ep.Event)
            .WithMany(e => e.EventParticipants)
            .HasForeignKey(ep => ep.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ep => ep.User)
            .WithMany(u => u.Events)
            .HasForeignKey(ep => ep.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(ep => ep.Status);
        builder.HasIndex(ep => ep.Role);
    }
}
