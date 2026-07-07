using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PRN232_be.Models.Configurations
{
    public class GradeComponentConfiguration : IEntityTypeConfiguration<GradeComponent>
    {
        public void Configure(EntityTypeBuilder<GradeComponent> builder)
        {
            builder.ToTable("grade_components");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Code).IsRequired().HasMaxLength(100);
            builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
            builder.Property(x => x.Weight).HasColumnType("decimal(18,2)");

            builder.HasIndex(x => new { x.CourseId, x.Code }).IsUnique();

            builder.HasOne(x => x.Course)
                .WithMany(c => c.GradeComponents)
                .HasForeignKey(x => x.CourseId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasQueryFilter(x => !x.IsDeleted);
        }
    }
}
