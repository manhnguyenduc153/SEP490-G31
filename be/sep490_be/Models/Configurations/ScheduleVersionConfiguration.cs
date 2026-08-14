using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using sep490_be.Models;

namespace sep490_be.Models.Configurations
{
    public class ScheduleVersionConfiguration : IEntityTypeConfiguration<ScheduleVersion>
    {
        public void Configure(EntityTypeBuilder<ScheduleVersion> builder)
        {
            builder.ToTable("schedule_versions");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
            builder.Property(x => x.ScheduleJson).HasColumnType("nvarchar(max)").IsRequired();

            builder.HasOne(x => x.Semester)
                .WithMany()
                .HasForeignKey(x => x.SemesterId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasQueryFilter(x => !x.IsDeleted);
        }
    }
}
