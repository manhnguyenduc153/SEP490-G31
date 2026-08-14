using System;
using System.Collections.Generic;

namespace sep490_be.DTO.Class
{
    /// <summary>
    /// Snapshot content persisted into ScheduleVersion.ScheduleJson — a point-in-time
    /// capture of a semester's live Planning-status classes/schedules/enrollments.
    /// </summary>
    public class ScheduleVersionSnapshotDto
    {
        public int SemesterId { get; set; }
        public List<ClassDraftSaveDto> Classes { get; set; } = new();
    }

    public class SaveScheduleVersionRequestDto
    {
        public string Name { get; set; } = string.Empty;
    }

    public class ScheduleVersionListItemDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public int ClassCount { get; set; }
        public bool IsAutoSaved { get; set; }
    }
}
