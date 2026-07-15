using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace sep490_be.Models.Configurations
{
    public class ParentStudentConfiguration : IEntityTypeConfiguration<ParentStudent>
    {
        public void Configure(EntityTypeBuilder<ParentStudent> builder)
        {
            builder.ToTable("parent_students");
            builder.HasKey(x => x.Id);

            // StandardEntity fields
            builder.Property(x => x.Code).IsRequired().HasMaxLength(50);
            builder.Property(x => x.Name).IsRequired().HasMaxLength(200);  // Tên phụ huynh
            builder.Property(x => x.TextSearch).HasMaxLength(500);

            // ParentStudent-specific fields
            builder.Property(x => x.ParentPhone).HasMaxLength(20);
            builder.Property(x => x.Email).HasMaxLength(150);
            builder.Property(x => x.UserId).HasMaxLength(450);   // IdentityUser Id là GUID string

            // ⭐ Soft-delete global filter — BẮT BUỘC
            builder.HasQueryFilter(x => !x.IsDeleted);
        }
    }
}
