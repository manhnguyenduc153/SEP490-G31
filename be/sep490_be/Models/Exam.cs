using System;
using System.Collections.Generic;
using sep490_be.Models.BaseEntities;

namespace sep490_be.Models
{
    public class Exam : StandardEntity<int>
    {
        public int? ClassId { get; set; }
        public int? ScheduleId { get; set; }
        public int Type { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
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

        public virtual Class? Class { get; set; }
        public virtual ClassSchedule? ClassSchedule { get; set; }
        public virtual ICollection<ExamQuestion> ExamQuestions { get; set; } = new List<ExamQuestion>();
        public virtual ICollection<ExamAttempt> ExamAttempts { get; set; } = new List<ExamAttempt>();
        public virtual ICollection<ExamSchedule> ExamSchedules { get; set; } = new List<ExamSchedule>();
        public virtual ICollection<StudentGrade> StudentGrades { get; set; } = new List<StudentGrade>();
    }
}

