using System;
using System.Reflection;

namespace sep490_be.Enums
{
    [AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
    public sealed class StringValueAttribute : Attribute
    {
        public string Value { get; }

        public StringValueAttribute(string value)
        {
            Value = value;
        }
    }

    public static class EnumExtensions
    {
        public static string GetStringValue(this Enum value)
        {
            Type type = value.GetType();
            FieldInfo? fieldInfo = type.GetField(value.ToString());
            if (fieldInfo != null)
            {
                var attribs = fieldInfo.GetCustomAttributes(typeof(StringValueAttribute), false) as StringValueAttribute[];
                if (attribs != null && attribs.Length > 0)
                {
                    return attribs[0].Value;
                }
            }
            return value.ToString();
        }
    }

    public enum StudentStatus
    {
        [StringValue("Inactive")]
        Inactive = 0,
        [StringValue("Active")]
        Active = 1,
        [StringValue("Suspended")]
        Suspended = 2,
        [StringValue("Graduated")]
        Graduated = 3
    }

    public enum TeacherStatus
    {
        [StringValue("Inactive")]
        Inactive = 0,
        [StringValue("Active")]
        Active = 1,
        [StringValue("On Leave")]
        OnLeave = 2
    }

    public enum GeneralStatus
    {
        [StringValue("Inactive")]
        Inactive = 0,
        [StringValue("Active")]
        Active = 1
    }

    public enum ClassStatus
    {
        [StringValue("Planning")]
        Planning = 0,
        [StringValue("Active")]
        Active = 1,
        [StringValue("Completed")]
        Completed = 2,
        [StringValue("Cancelled")]
        Cancelled = 3
    }

    public enum StudentClassStatus
    {
        [StringValue("Enrolled")]
        Enrolled = 0,
        [StringValue("Studying")]
        Studying = 1,
        [StringValue("Completed")]
        Completed = 2,
        [StringValue("Dropped")]
        Dropped = 3
    }

    public enum ClassScheduleStatus
    {
        [StringValue("Scheduled")]
        Scheduled = 0,
        [StringValue("On Going")]
        OnGoing = 1,
        [StringValue("Completed")]
        Completed = 2,
        [StringValue("Cancelled")]
        Cancelled = 3
    }

    public enum AttendanceStatus
    {
        [StringValue("Absent")]
        Absent = 0,
        [StringValue("Present")]
        Present = 1,
        [StringValue("Late")]
        Late = 2,
        [StringValue("Excused")]
        Excused = 3
    }

    public enum NotificationStatus
    {
        [StringValue("Unread")]
        Unread = 0,
        [StringValue("Read")]
        Read = 1
    }

    public enum NotificationTargetType
    {
        [StringValue("All")]
        All = 1,
        [StringValue("Class")]
        Class = 2,
        [StringValue("User")]
        User = 3
    }

    public enum QuestionType
    {
        [StringValue("Single Choice")]
        SingleChoice = 1,
        [StringValue("Multiple Choice")]
        MultipleChoice = 2,
        [StringValue("Essay")]
        Essay = 3,
        [StringValue("True or False")]
        TrueFalse = 4,
        [StringValue("Audio Recording")]
        AudioRecord = 5
    }

    public enum DifficultyLevel
    {
        [StringValue("Easy")]
        Easy = 1,
        [StringValue("Medium")]
        Medium = 2,
        [StringValue("Hard")]
        Hard = 3
    }

    public enum SkillType
    {
        [StringValue("Listening")]
        Listening = 1,
        [StringValue("Reading")]
        Reading = 2,
        [StringValue("Speaking")]
        Speaking = 3,
        [StringValue("Writing")]
        Writing = 4
    }

    public enum ExamType
    {
        [StringValue("Assignment")]
        Assignment = 1,
        [StringValue("Quiz")]
        Quiz = 2,
        [StringValue("Exam")]
        Exam = 3
    }

    public enum ExamStatus
    {
        [StringValue("Draft")]
        Draft = 0,
        [StringValue("Published")]
        Published = 1,
        [StringValue("Closed")]
        Closed = 2
    }

    public enum ExamAttemptStatus
    {
        [StringValue("Incomplete")]
        Incomplete = 0,
        [StringValue("Submitted")]
        Submitted = 1,
        [StringValue("Graded")]
        Graded = 2
    }

    public enum ExamScheduleStatus
    {
        [StringValue("Scheduled")]
        Scheduled = 0,
        [StringValue("In Progress")]
        InProgress = 1,
        [StringValue("Finished")]
        Finished = 2,
        [StringValue("Cancelled")]
        Cancelled = 3
    }

    public enum ExamStudentStatus
    {
        [StringValue("Registered")]
        Registered = 0,
        [StringValue("Attended")]
        Attended = 1,
        [StringValue("Absent")]
        Absent = 2
    }

    public enum GradeLevel
    {
        [StringValue("Foundation")]
        Foundation = 1,
        [StringValue("Pre-IELTS")]
        PreIelts = 2,
        [StringValue("IELTS 4.0 - 5.0")]
        Ielts4_5 = 3,
        [StringValue("IELTS 5.0 - 6.0")]
        Ielts5_6 = 4,
        [StringValue("IELTS 6.0 - 6.5")]
        Ielts6_65 = 5,
        [StringValue("IELTS 6.5+")]
        Ielts65Plus = 6
    }

    public enum SemesterStatus
    {
        [StringValue("Active/Registering")]
        Active = 1,
        [StringValue("Ongoing")]
        Ongoing = 2,
        [StringValue("Completed")]
        Completed = 3
    }

    public enum StudentRegistrationStatus
    {
        [StringValue("Pending")]
        Pending = 0,
        [StringValue("Scheduled")]
        Scheduled = 1,
        [StringValue("Cancelled")]
        Cancelled = 2
    }
}

