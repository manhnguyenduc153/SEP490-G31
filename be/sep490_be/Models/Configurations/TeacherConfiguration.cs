using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace sep490_be.Models.Configurations
{
    public class TeacherConfiguration : IEntityTypeConfiguration<Teacher>
    {
        public void Configure(EntityTypeBuilder<Teacher> builder)
        {
            builder.ToTable("teachers");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Code).IsRequired().HasMaxLength(50);
            builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
            builder.Property(x => x.Dob).HasColumnType("date");
            builder.Property(x => x.Email).HasMaxLength(150);
            builder.Property(x => x.Phone).HasMaxLength(20);
            builder.Property(x => x.Address).HasMaxLength(500);
            builder.Property(x => x.Certificate).HasColumnType("nvarchar(max)");
            builder.Property(x => x.Avatar).HasMaxLength(500);

            // Soft-delete global filter
            builder.HasQueryFilter(x => !x.IsDeleted);
        }
    }
}

