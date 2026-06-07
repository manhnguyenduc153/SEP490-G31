using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PRN232_be.Models.Configurations
{
    public class ParentStudentConfiguration : IEntityTypeConfiguration<ParentStudent>
    {
        public void Configure(EntityTypeBuilder<ParentStudent> builder)
        {
            builder.ToTable("parent_students");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.ParentName).HasMaxLength(200);
            builder.Property(x => x.ParentPhone).HasMaxLength(20);
            builder.Property(x => x.Relationship).HasMaxLength(50);

            builder.HasOne(x => x.Student)
                .WithMany(s => s.ParentStudents)
                .HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
