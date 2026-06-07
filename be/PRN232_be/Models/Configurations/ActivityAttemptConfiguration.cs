using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PRN232_be.Models.Configurations
{
    public class ActivityAttemptConfiguration : IEntityTypeConfiguration<ActivityAttempt>
    {
        public void Configure(EntityTypeBuilder<ActivityAttempt> builder)
        {
            builder.ToTable("activity_attempts");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Code).IsRequired().HasMaxLength(50);
            builder.Property(x => x.Name).IsRequired().HasMaxLength(200);

            builder.Property(x => x.Score).HasColumnType("decimal(18,2)");

            builder.HasOne(x => x.Activity)
                .WithMany(a => a.ActivityAttempts)
                .HasForeignKey(x => x.ActivityId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.Student)
                .WithMany(s => s.ActivityAttempts)
                .HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            // Soft-delete global filter
            builder.HasQueryFilter(x => !x.IsDeleted);
        }
    }
}
