using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PRN232_be.Models.Configurations
{
    public class ExamQuestionConfiguration : IEntityTypeConfiguration<ExamQuestion>
    {
        public void Configure(EntityTypeBuilder<ExamQuestion> builder)
        {
            builder.ToTable("exam_questions");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Point).HasColumnType("decimal(18,2)");

            builder.HasOne(x => x.Exam)
                .WithMany(a => a.ExamQuestions)
                .HasForeignKey(x => x.ExamId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.Question)
                .WithMany(q => q.ExamQuestions)
                .HasForeignKey(x => x.QuestionId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
