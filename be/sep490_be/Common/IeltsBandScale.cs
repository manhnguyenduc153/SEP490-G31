using System;
using System.Linq;
using sep490_be.Models;

namespace sep490_be.Common
{
    // Official IELTS raw-score-to-band conversion. Listening and Reading (Academic) share
    // identical thresholds in the published scale, so one table covers both skills.
    public static class IeltsBandScale
    {
        public const int ListeningSkillType = 1;
        public const int ReadingSkillType = 2;
        public const int SpeakingSkillType = 3;
        public const int WritingSkillType = 4;

        // Official IELTS Listening/Reading papers have 40 questions; real exams in this system
        // rarely do, so correct counts are scaled to this equivalent before the table lookup.
        public const int StandardQuestionCount = 40;
        public const decimal SpeakingWritingMinBand = 0m;
        public const decimal SpeakingWritingMaxBand = 9m;

        private static readonly (int MinCorrect, int MaxCorrect, decimal Band)[] CorrectCountToBand = new[]
        {
            (39, 40, 9.0m),
            (37, 38, 8.5m),
            (35, 36, 8.0m),
            (33, 34, 7.5m),
            (30, 32, 7.0m),
            (27, 29, 6.5m),
            (23, 26, 6.0m),
            (20, 22, 5.5m),
            (16, 19, 5.0m),
            (13, 15, 4.5m),
            (10, 12, 4.0m),
            (7, 9, 3.5m),
            (5, 6, 3.0m),
            (3, 4, 2.5m),
        };

        private static decimal LookupBand(int correctCount)
        {
            foreach (var (min, max, band) in CorrectCountToBand)
            {
                if (correctCount >= min && correctCount <= max) return band;
            }

            var topOfScale = CorrectCountToBand[0];
            if (correctCount > topOfScale.MaxCorrect) return topOfScale.Band;

            return CorrectCountToBand[^1].Band; // below the lowest published range -> floor at the lowest band
        }

        // Real exams in this system don't always have exactly 40 questions, so the raw
        // correct-count is scaled to a /40-equivalent before hitting the official table.
        // Unanswered questions count against the student (totalQuestions is the exam's full
        // question count, not just how many were answered).
        public static decimal? GetBandForListeningReading(int correctCount, int totalQuestions)
        {
            if (totalQuestions <= 0) return null;

            var scaledCorrect = (int)Math.Round((decimal)correctCount / totalQuestions * StandardQuestionCount, MidpointRounding.AwayFromZero);
            scaledCorrect = Math.Max(0, Math.Min(StandardQuestionCount, scaledCorrect));
            return LookupBand(scaledCorrect);
        }

        // Returns the single skill type shared by every question in the exam, or null when the
        // exam mixes skills (or has no questions) — mirrors the homogeneity check already used
        // by StudentGradeRepository.CalculateExamSkillScoresAsync and the frontend's getExamSkillCode.
        public static int? GetSingleSkillType(Exam exam)
        {
            var skills = exam.ExamQuestions
                .Where(eq => eq.Question != null)
                .Select(eq => eq.Question.SkillType)
                .Distinct()
                .ToList();

            return skills.Count == 1 ? skills[0] : (int?)null;
        }

        // Round to the nearest IELTS half-band. Quarter-band boundaries round up:
        // 6.25 -> 6.5, values below 6.25 -> 6.0, and 6.75 -> 7.0.
        public static decimal RoundToHalfBand(decimal raw)
        {
            var clamped = Math.Max(SpeakingWritingMinBand, Math.Min(SpeakingWritingMaxBand, raw));
            return Math.Floor(clamped * 2m + 0.5m) / 2m;
        }

        // Speaking/Writing are graded directly on the 0-9 band scale by the teacher; the entered
        // score IS the band, just clamped to range and normalized to the official half-band grid.
        public static decimal RoundSpeakingWritingBand(decimal rawScore)
        {
            return RoundToHalfBand(rawScore);
        }

        // ExamAttempt.Score is now stored directly in band units for all 4 skills (Listening/Reading
        // via GetBandForListeningReading at grading time, Speaking/Writing via direct 0-9 grading),
        // so this just mirrors Score for band-graded exams and hides it for legacy mixed-skill ones.
        public static decimal? ComputeAttemptBand(Exam exam, decimal? score)
        {
            if (!score.HasValue) return null;

            var skill = GetSingleSkillType(exam);
            return skill is ListeningSkillType or ReadingSkillType or SpeakingSkillType or WritingSkillType
                ? score
                : null;
        }
    }
}
