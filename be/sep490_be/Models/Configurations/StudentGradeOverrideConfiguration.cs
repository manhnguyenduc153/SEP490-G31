using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PRN232_be.Models.Configurations
{
    public class StudentGradeOverrideConfiguration : IEntityTypeConfiguration<StudentGradeOverride>
    {
        public void Configure(EntityTypeBuilder<StudentGradeOverride> builder)
        {
            builder.ToTable("student_grade_overrides");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Score).HasColumnType("decimal(18,2)");
            builder.HasIndex(x => new { x.StudentClassId, x.GradeComponentId }).IsUnique();

            builder.HasOne(x => x.StudentClass)
                .WithMany(sc => sc.StudentGradeOverrides)
                .HasForeignKey(x => x.StudentClassId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.GradeComponent)
                .WithMany(gc => gc.StudentGradeOverrides)
                .HasForeignKey(x => x.GradeComponentId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasQueryFilter(x => !x.IsDeleted);
        }
    }
}
