using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace sep490_be.Models.Configurations
{
    public class StudentGradeConfiguration : IEntityTypeConfiguration<StudentGrade>
    {
        public void Configure(EntityTypeBuilder<StudentGrade> builder)
        {
            builder.ToTable("student_grades");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.FinalScore).HasColumnType("decimal(18,2)");
            builder.Property(x => x.Weight).HasColumnType("decimal(18,2)");
            builder.Property(x => x.Note).HasMaxLength(1000);

            builder.HasOne(x => x.StudentClass)
                .WithMany(sc => sc.StudentGrades)
                .HasForeignKey(x => x.StudentClassId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.Exam)
                .WithMany(a => a.StudentGrades)
                .HasForeignKey(x => x.ExamId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}

