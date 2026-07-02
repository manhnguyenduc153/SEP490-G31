namespace PRN232_be.Enums
{
    /// <summary>
    /// Hard-coded fixed time slots for the scheduling system.
    /// These are the canonical time ranges used by OR-Tools and displayed in the UI.
    /// Do NOT change the order – the index is used as the slot identifier in the solver.
    /// </summary>
    public record FixedTimeSlot(int Index, string Name, TimeSpan Start, TimeSpan End)
    {
        public static readonly FixedTimeSlot[] All = new[]
        {
            new FixedTimeSlot(0, "Ca 1 (07:30 - 09:30)", new TimeSpan(7,  30, 0), new TimeSpan(9,  30, 0)),
            new FixedTimeSlot(1, "Ca 2 (10:00 - 12:00)", new TimeSpan(10,  0, 0), new TimeSpan(12,  0, 0)),
            new FixedTimeSlot(2, "Ca 3 (13:00 - 15:00)", new TimeSpan(13,  0, 0), new TimeSpan(15,  0, 0)),
            new FixedTimeSlot(3, "Ca 4 (16:00 - 18:00)", new TimeSpan(16,  0, 0), new TimeSpan(18,  0, 0)),
            new FixedTimeSlot(4, "Ca 5 (19:00 - 21:00)", new TimeSpan(19,  0, 0), new TimeSpan(21,  0, 0)),
        };

        /// <summary>
        /// Find the fixed slot whose start time matches (used when looking up from DB TimeSlot).
        /// Returns null if the time does not match any canonical slot.
        /// </summary>
        public static FixedTimeSlot? FromStartTime(TimeSpan start) =>
            Array.Find(All, fs => fs.Start == start);

        /// <summary>Formatted "HH:mm" start string, e.g. "07:30".</summary>
        public string StartStr => Start.ToString(@"hh\:mm");

        /// <summary>Formatted "HH:mm" end string, e.g. "09:30".</summary>
        public string EndStr   => End.ToString(@"hh\:mm");
    }
}
