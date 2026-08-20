using System;
using System.Collections.Generic;
using sep490_be.DTO.Question;

namespace sep490_be.DTO.Exam
{
    public class ExamDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int? ClassId { get; set; }
        public string? ClassName { get; set; }
        public int? ScheduleId { get; set; }
        public int Type { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int? Duration { get; set; }
        public decimal? TotalScore { get; set; }
        public decimal? PassingScore { get; set; }
        public int? MaxAttempts { get; set; }
        public bool AllowLateSubmit { get; set; }
        public bool ShuffleQuestion { get; set; }
        public bool ShowAnswerAfter { get; set; }
        public int Status { get; set; }
        public DateTime CreatedAt { get; set; }
        
        public int QuestionCount { get; set; }
        public int SubmissionCount { get; set; }
        public decimal? LatestScore { get; set; }
        public bool IsGraded { get; set; }
        // Single skill shared by every question in the exam (see IeltsBandScale.GetSingleSkillType),
        // null when mixed/unset. Lets clients know Score/LatestScore is already an IELTS band (0-9)
        // without needing the full Questions list.
        public int? SkillType { get; set; }
        public List<int> QuestionIds { get; set; } = new List<int>();
        public List<QuestionDto> Questions { get; set; } = new List<QuestionDto>();
    }
}

