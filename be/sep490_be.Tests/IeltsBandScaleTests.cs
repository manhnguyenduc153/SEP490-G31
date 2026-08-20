using FluentAssertions;
using sep490_be.Common;
using sep490_be.Models;

namespace sep490_be.Tests.Services
{
    public class IeltsBandScaleTests
    {
        [Theory]
        [InlineData(40, 40, 9.0)]
        [InlineData(39, 40, 9.0)]
        [InlineData(38, 40, 8.5)]
        [InlineData(37, 40, 8.5)]
        [InlineData(4, 40, 2.5)]
        [InlineData(3, 40, 2.5)]
        [InlineData(2, 40, 2.5)] // below lowest published range -> floors at the lowest band
        [InlineData(0, 40, 2.5)]
        public void GetBandForListeningReading_ExactlyFortyQuestions_MatchOfficialScale(int correctCount, int totalQuestions, decimal expectedBand)
        {
            IeltsBandScale.GetBandForListeningReading(correctCount, totalQuestions).Should().Be(expectedBand);
        }

        [Theory]
        [InlineData(6, 8, 7.0)] // scaled to 6/8*40 = 30 correct-equivalent -> band 7.0
        [InlineData(20, 25, 7.0)] // scaled to 20/25*40 = 32 -> band 7.0
        [InlineData(0, 8, 2.5)] // floors at the lowest band even with a tiny exam
        [InlineData(1, 1, 9.0)] // perfect score on any size exam scales to the top band
        public void GetBandForListeningReading_NonStandardQuestionCount_ScalesToFortyEquivalent(int correctCount, int totalQuestions, decimal expectedBand)
        {
            IeltsBandScale.GetBandForListeningReading(correctCount, totalQuestions).Should().Be(expectedBand);
        }

        [Fact]
        public void GetBandForListeningReading_ZeroQuestions_ReturnsNull()
        {
            IeltsBandScale.GetBandForListeningReading(0, 0).Should().BeNull();
        }

        [Theory]
        [InlineData(6.25, 6.5)] // exact quarter band always resolves to the half band...
        [InlineData(6.75, 6.5)] // ...on both sides: .25 rounds up, .75 rounds down, never to a whole band
        [InlineData(6.1, 6.0)]
        [InlineData(6.3, 6.5)]
        [InlineData(6.0, 6.0)]
        public void RoundToHalfBand_QuarterBandsResolveToTheNearbyHalfBand(decimal raw, decimal expected)
        {
            IeltsBandScale.RoundToHalfBand(raw).Should().Be(expected);
        }

        [Theory]
        [InlineData(9.5, 9.0)] // clamps above the 0-9 band ceiling
        [InlineData(-1, 0.0)]
        [InlineData(7.25, 7.5)]
        [InlineData(7.75, 7.5)]
        public void RoundSpeakingWritingBand_ClampsToZeroNineThenRounds(decimal raw, decimal expected)
        {
            IeltsBandScale.RoundSpeakingWritingBand(raw).Should().Be(expected);
        }

        private static Exam BuildExam(int skillType, int questionCount, decimal pointPerQuestion = 1m)
        {
            var exam = new Exam { TotalScore = questionCount * pointPerQuestion };
            for (var i = 0; i < questionCount; i++)
            {
                exam.ExamQuestions.Add(new ExamQuestion
                {
                    Point = pointPerQuestion,
                    Question = new Question { SkillType = skillType }
                });
            }
            return exam;
        }

        [Fact]
        public void GetSingleSkillType_MixedSkillExam_ReturnsNull()
        {
            var exam = new Exam();
            exam.ExamQuestions.Add(new ExamQuestion { Point = 1m, Question = new Question { SkillType = IeltsBandScale.ListeningSkillType } });
            exam.ExamQuestions.Add(new ExamQuestion { Point = 1m, Question = new Question { SkillType = IeltsBandScale.ReadingSkillType } });

            IeltsBandScale.GetSingleSkillType(exam).Should().BeNull();
        }

        [Theory]
        [InlineData(IeltsBandScale.ListeningSkillType)]
        [InlineData(IeltsBandScale.ReadingSkillType)]
        [InlineData(IeltsBandScale.SpeakingSkillType)]
        [InlineData(IeltsBandScale.WritingSkillType)]
        public void ComputeAttemptBand_BandGradedExam_MirrorsStoredScore(int skillType)
        {
            // Score is already stored as the band at grading time for all 4 skills, regardless of
            // how many questions the exam has, so ComputeAttemptBand just reflects it back.
            var exam = BuildExam(skillType, 8);
            IeltsBandScale.ComputeAttemptBand(exam, 6.5m).Should().Be(6.5m);
        }

        [Fact]
        public void ComputeAttemptBand_MixedSkillExam_ReturnsNull()
        {
            var exam = new Exam();
            exam.ExamQuestions.Add(new ExamQuestion { Point = 1m, Question = new Question { SkillType = IeltsBandScale.ListeningSkillType } });
            exam.ExamQuestions.Add(new ExamQuestion { Point = 1m, Question = new Question { SkillType = IeltsBandScale.WritingSkillType } });

            IeltsBandScale.ComputeAttemptBand(exam, 8m).Should().BeNull();
        }

        [Fact]
        public void ComputeAttemptBand_NullScore_ReturnsNull()
        {
            var exam = BuildExam(IeltsBandScale.ListeningSkillType, 40);
            IeltsBandScale.ComputeAttemptBand(exam, null).Should().BeNull();
        }
    }
}
