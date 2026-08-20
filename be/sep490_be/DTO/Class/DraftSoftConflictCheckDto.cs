using System.Collections.Generic;

namespace sep490_be.DTO.Class
{
    /// <summary>
    /// Request DTO to check student-preference soft conflicts for a draft schedule move.
    /// </summary>
    public class DraftSoftConflictCheckDto
    {
        public int SemesterId { get; set; }
        public int CourseId { get; set; }
        /// <summary>List of studentIds enrolled in the draft class.</summary>
        public List<int> StudentIds { get; set; } = new();
        /// <summary>Target day-of-week (0 = Sunday, 1 = Monday, ... 6 = Saturday).</summary>
        public int TargetDayOfWeek { get; set; }
        /// <summary>Target slot index (0–4, matching FixedTimeSlot.All).</summary>
        public int TargetSlotIndex { get; set; }
    }
}
