using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PRN232_be.Models.Configurations
{
    public class ActivityAnswerConfiguration : IEntityTypeConfiguration<ActivityAnswer>
    {
        public void Configure(EntityTypeBuilder<ActivityAnswer> builder)
        {
            builder.ToTable("activity_answers");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Code).IsRequired().HasMaxLength(50);
            builder.Property(x => x.Name).IsRequired().HasMaxLength(200);

            builder.Property(x => x.AttachmentUrl).HasMaxLength(500);
            builder.Property(x => x.TeacherComment).HasMaxLength(1000);
            builder.Property(x => x.Score).HasColumnType("decimal(18,2)");

            builder.HasOne(x => x.ActivityAttempt)
                .WithMany(aa => aa.ActivityAnswers)
                .HasForeignKey(x => x.AttemptId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.Question)
                .WithMany(q => q.ActivityAnswers)
                .HasForeignKey(x => x.QuestionId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.Teacher)
                .WithMany(t => t.ActivityAnswers)
                .HasForeignKey(x => x.GradedBy)
                .OnDelete(DeleteBehavior.Restrict);

            // Soft-delete global filter
            builder.HasQueryFilter(x => !x.IsDeleted);
        }
    }
}
