using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace sep490_be.Models.Configurations
{
    public class StudentRegistrationConfiguration : IEntityTypeConfiguration<StudentRegistration>
    {
        public void Configure(EntityTypeBuilder<StudentRegistration> builder)
        {
            builder.ToTable("student_registrations");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.PreferredSlotsJson).HasColumnType("nvarchar(max)");

            builder.HasOne(x => x.Student)
                .WithMany()
                .HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.Course)
                .WithMany()
                .HasForeignKey(x => x.CourseId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.Semester)
                .WithMany(s => s.StudentRegistrations)
                .HasForeignKey(x => x.SemesterId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}

