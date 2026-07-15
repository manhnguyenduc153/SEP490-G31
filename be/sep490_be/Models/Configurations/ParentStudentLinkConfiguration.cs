using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace sep490_be.Models.Configurations
{
    public class ParentStudentLinkConfiguration : IEntityTypeConfiguration<ParentStudentLink>
    {
        public void Configure(EntityTypeBuilder<ParentStudentLink> builder)
        {
            builder.ToTable("parent_student_links");
            builder.HasKey(x => x.Id);

            // Specific fields
            builder.Property(x => x.Relationship).HasMaxLength(50);

            // Soft-delete global filter
            builder.HasQueryFilter(x => !x.IsDeleted);

            // Relationships
            builder.HasOne(x => x.Parent)
                .WithMany(p => p.ParentStudentLinks)
                .HasForeignKey(x => x.ParentId)
                .OnDelete(DeleteBehavior.Cascade); // Delete mappings if Parent is deleted

            builder.HasOne(x => x.Student)
                .WithMany(s => s.ParentStudentLinks)
                .HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
