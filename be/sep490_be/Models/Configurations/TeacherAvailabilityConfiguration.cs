using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace sep490_be.Models.Configurations
{
    public class TeacherAvailabilityConfiguration : IEntityTypeConfiguration<TeacherAvailability>
    {
        public void Configure(EntityTypeBuilder<TeacherAvailability> builder)
        {
            builder.ToTable("teacher_availabilities");
            builder.HasKey(x => x.Id);
            
            builder.HasOne(x => x.Teacher)
                .WithMany()
                .HasForeignKey(x => x.TeacherId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.Semester)
                .WithMany(s => s.TeacherAvailabilities)
                .HasForeignKey(x => x.SemesterId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}

