using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PRN232_be.Models.Configurations
{
    public class QuestionConfiguration : IEntityTypeConfiguration<Question>
    {
        public void Configure(EntityTypeBuilder<Question> builder)
        {
            builder.ToTable("questions");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Code).IsRequired().HasMaxLength(50);
            builder.Property(x => x.Name).IsRequired().HasMaxLength(200);

            builder.Property(x => x.Content).IsRequired();
            builder.Property(x => x.Point).HasColumnType("decimal(18,2)");

            builder.HasOne(x => x.QuestionCategory)
                .WithMany(qc => qc.Questions)
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            // Soft-delete global filter
            builder.HasQueryFilter(x => !x.IsDeleted);
        }
    }
}
