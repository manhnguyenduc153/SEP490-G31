using System;
using System.Collections.Generic;

namespace sep490_be.DTO.Class
{
    public class ConflictCheckResultDto
    {
        public bool HasConflict { get; set; }
        public List<ConflictDetailDto> Conflicts { get; set; } = new List<ConflictDetailDto>();
        public List<StudentPreferenceWarningDto> SoftWarnings { get; set; } = new List<StudentPreferenceWarningDto>();
    }

    public class ConflictDetailDto
    {
        public string Type { get; set; } = string.Empty; // "Teacher" or "Room"
        public int? TeacherId { get; set; }
        public string? TeacherName { get; set; }
        public int? RoomId { get; set; }
        public string? RoomName { get; set; }
        public DateTime? Date { get; set; }
        public int? SlotId { get; set; }
        public string? SlotName { get; set; }
        public string? StartTime { get; set; }
        public string? EndTime { get; set; }
        public int? ConflictClassId { get; set; }
        public string? ConflictClassCode { get; set; }
        public string? ConflictClassName { get; set; }
    }
}

