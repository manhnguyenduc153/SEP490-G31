using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace sep490_be.Models.Configurations
{
    public class StudentConfiguration : IEntityTypeConfiguration<Student>
    {
        public void Configure(EntityTypeBuilder<Student> builder)
        {
            builder.ToTable("students");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Code).IsRequired().HasMaxLength(50);
            builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
            builder.Property(x => x.Dob).HasColumnType("date");
            builder.Property(x => x.Email).HasMaxLength(150);
            builder.Property(x => x.Phone).HasMaxLength(20);
            builder.Property(x => x.Address).HasMaxLength(500);
            builder.Property(x => x.SchoolName).HasMaxLength(200);
            builder.Property(x => x.ParentName).HasMaxLength(200);
            builder.Property(x => x.ParentPhone).HasMaxLength(20);

            // Soft-delete global filter
            builder.HasQueryFilter(x => !x.IsDeleted);
        }
    }
}

