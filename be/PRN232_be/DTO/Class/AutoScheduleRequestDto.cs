using System.Collections.Generic;

namespace PRN232_be.DTO.Class
{
    /// <summary>
    /// Request body for POST /api/Class/auto-schedule
    /// </summary>
    public class AutoScheduleRequestDto
    {
        /// <summary>IDs of the classes to auto-schedule.</summary>
        public List<int> ClassIds { get; set; } = new();

        /// <summary>Scheduling constraints chosen by the user in the UI.</summary>
        public AutoScheduleConstraintDto Constraints { get; set; } = new();
    }

    /// <summary>
    /// User-defined constraints that are injected into the CP-SAT model.
    /// </summary>
    public class AutoScheduleConstraintDto
    {
        /// <summary>
        /// Number of weekly sessions per class (applies only to classes that have no existing schedule).
        /// Range: 1–3. Default = 2.
        /// </summary>
        public int SessionsPerWeek { get; set; } = 2;

        /// <summary>
        /// Allowed time-of-day buckets. Must contain at least one of: "morning", "afternoon", "evening".
        /// Mapping to FixedTimeSlot indices:
        ///   morning   → slots 0, 1  (Ca 1: 07:30, Ca 2: 10:00)
        ///   afternoon → slots 2, 3  (Ca 3: 13:00, Ca 4: 16:00)
        ///   evening   → slot  4     (Ca 5: 19:00)
        /// Default = all three (no restriction).
        /// </summary>
        public List<string> TimePreferences { get; set; } = new() { "morning", "afternoon", "evening" };

        /// <summary>
        /// When false (default) the solver enforces a minimum 1-day gap between sessions of the
        /// same class (i.e. dayVar[j+1] >= dayVar[j] + 2).
        /// When true, consecutive days are allowed (dayVar[j+1] > dayVar[j]).
        /// </summary>
        public bool AllowConsecutiveDays { get; set; } = false;

        /// <summary>
        /// When false (default) only Mon–Fri (1–5) are considered.
        /// When true Sat (6) and Sun (0) are also available.
        /// </summary>
        public bool AllowWeekend { get; set; } = false;
    }
}
