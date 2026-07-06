using System;
using System.Collections.Generic;

namespace sep490_be.DTO.Exam
{
    public class ExamSaveDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int? ClassId { get; set; }
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
        public List<int> QuestionIds { get; set; } = new List<int>();
    }
}

