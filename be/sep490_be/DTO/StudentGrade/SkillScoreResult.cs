using System;
using System.Collections.Generic;

namespace sep490_be.DTO.StudentGrade
{
    // Scores: 0-10 average per skill (existing behavior). Bands: average IELTS band per skill,
    // only populated for skills/exams where a band could be computed (see IeltsBandScale).
    public class SkillScoreResult
    {
        public Dictionary<string, decimal> Scores { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, decimal> Bands { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
