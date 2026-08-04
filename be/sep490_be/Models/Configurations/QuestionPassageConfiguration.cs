using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace sep490_be.Models.Configurations
{
    public class QuestionPassageConfiguration : IEntityTypeConfiguration<QuestionPassage>
    {
        public void Configure(EntityTypeBuilder<QuestionPassage> builder)
        {
            builder.ToTable("question_passages");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Code).IsRequired().HasMaxLength(50);
            builder.Property(x => x.Title).IsRequired().HasMaxLength(300);

            builder.HasOne(x => x.QuestionCategory)
                   .WithMany()
                   .HasForeignKey(x => x.CategoryId)
                   .OnDelete(DeleteBehavior.SetNull);

            builder.HasMany(x => x.Questions)
                   .WithOne(x => x.QuestionPassage)
                   .HasForeignKey(x => x.PassageId)
                   .OnDelete(DeleteBehavior.SetNull);

            builder.HasQueryFilter(x => !x.IsDeleted);
        }
    }
}
