using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace sep490_be.Models.Configurations
{
    public class StudentClassConfiguration : IEntityTypeConfiguration<StudentClass>
    {
        public void Configure(EntityTypeBuilder<StudentClass> builder)
        {
            builder.ToTable("student_classes");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.EnrollDate).HasColumnType("date");
            builder.Property(x => x.CompletionDate).HasColumnType("date");
            builder.Property(x => x.Description).HasMaxLength(1000);

            builder.HasOne(x => x.Student)
                .WithMany(s => s.StudentClasses)
                .HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.Class)
                .WithMany(c => c.StudentClasses)
                .HasForeignKey(x => x.ClassId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}

