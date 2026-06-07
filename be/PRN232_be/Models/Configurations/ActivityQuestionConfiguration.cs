using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PRN232_be.Models.Configurations
{
    public class ActivityQuestionConfiguration : IEntityTypeConfiguration<ActivityQuestion>
    {
        public void Configure(EntityTypeBuilder<ActivityQuestion> builder)
        {
            builder.ToTable("activity_questions");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Point).HasColumnType("decimal(18,2)");

            builder.HasOne(x => x.Activity)
                .WithMany(a => a.ActivityQuestions)
                .HasForeignKey(x => x.ActivityId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.Question)
                .WithMany(q => q.ActivityQuestions)
                .HasForeignKey(x => x.QuestionId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
