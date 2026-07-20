using System.Reflection;

namespace sep490_be.DTO.Common
{
    public static class Permissions
    {
        public static class User
        {
            public const string UserPage = "User";
            public const string User_View = "User.View";
            public const string User_Create = "User.Create";
            public const string User_Edit = "User.Edit";
            public const string User_Delete = "User.Delete";
        }

        public static class Role
        {
            public const string RolePage = "Role";
            public const string Role_View = "Role.View";
            public const string Role_Create = "Role.Create";
            public const string Role_Edit = "Role.Edit";
            public const string Role_Delete = "Role.Delete";
        }

        public static class Product
        {
            public const string ProductPage = "Product";
            public const string Product_View = "Product.View";
            public const string Product_Create = "Product.Create";
            public const string Product_Edit = "Product.Edit";
            public const string Product_Delete = "Product.Delete";
        }

        public static class Student
        {
            public const string StudentPage = "Student";
            public const string Student_View = "Student.View";
            public const string Student_Create = "Student.Create";
            public const string Student_Edit = "Student.Edit";
            public const string Student_Delete = "Student.Delete";
        }

        public static class Teacher
        {
            public const string TeacherPage = "Teacher";
            public const string Teacher_View = "Teacher.View";
            public const string Teacher_Create = "Teacher.Create";
            public const string Teacher_Edit = "Teacher.Edit";
            public const string Teacher_Delete = "Teacher.Delete";
        }

        public static class ParentStudent
        {
            public const string ParentStudentPage = "ParentStudent";
            public const string ParentStudent_View = "ParentStudent.View";
            public const string ParentStudent_Create = "ParentStudent.Create";
            public const string ParentStudent_Edit = "ParentStudent.Edit";
            public const string ParentStudent_Delete = "ParentStudent.Delete";
        }

        public static class Course
        {
            public const string CoursePage = "Course";
            public const string Course_View = "Course.View";
            public const string Course_Create = "Course.Create";
            public const string Course_Edit = "Course.Edit";
            public const string Course_Delete = "Course.Delete";
        }

        public static class Class
        {
            public const string ClassPage = "Class";
            public const string Class_View = "Class.View";
            public const string Class_Create = "Class.Create";
            public const string Class_Edit = "Class.Edit";
            public const string Class_Delete = "Class.Delete";
            public const string Class_Import = "Class.Import";
            public const string Class_StudentView = "Class.StudentView";
            public const string Class_TeacherView = "Class.TeacherView";
        }

        public static class Room
        {
            public const string RoomPage = "Room";
            public const string Room_View = "Room.View";
            public const string Room_Create = "Room.Create";
            public const string Room_Edit = "Room.Edit";
            public const string Room_Delete = "Room.Delete";
        }

        public static class TimeSlot
        {
            public const string TimeSlotPage = "TimeSlot";
            public const string TimeSlot_View = "TimeSlot.View";
            public const string TimeSlot_Create = "TimeSlot.Create";
            public const string TimeSlot_Edit = "TimeSlot.Edit";
            public const string TimeSlot_Delete = "TimeSlot.Delete";
        }

        public static class StudentClass
        {
            public const string StudentClassPage = "StudentClass";
            public const string StudentClass_View = "StudentClass.View";
            public const string StudentClass_Create = "StudentClass.Create";
            public const string StudentClass_Edit = "StudentClass.Edit";
            public const string StudentClass_Delete = "StudentClass.Delete";
        }

        public static class ClassSchedule
        {
            public const string ClassSchedulePage = "ClassSchedule";
            public const string ClassSchedule_View = "ClassSchedule.View";
            public const string ClassSchedule_Create = "ClassSchedule.Create";
            public const string ClassSchedule_Edit = "ClassSchedule.Edit";
            public const string ClassSchedule_Delete = "ClassSchedule.Delete";
            public const string ClassSchedule_StudentView = "ClassSchedule.StudentView";
            public const string ClassSchedule_TeacherView = "ClassSchedule.TeacherView";
        }

        public static class Attendance
        {
            public const string AttendancePage = "Attendance";
            public const string Attendance_View = "Attendance.View";
            public const string Attendance_Create = "Attendance.Create";
            public const string Attendance_Edit = "Attendance.Edit";
            public const string Attendance_Delete = "Attendance.Delete";
            public const string Attendance_SaveAttendance = "Attendance.SaveAttendance";
        }

        public static class LearningMaterial
        {
            public const string LearningMaterialPage = "LearningMaterial";
            public const string LearningMaterial_View = "LearningMaterial.View";
            public const string LearningMaterial_Create = "LearningMaterial.Create";
            public const string LearningMaterial_Edit = "LearningMaterial.Edit";
            public const string LearningMaterial_Delete = "LearningMaterial.Delete";
        }

        public static class Notification
        {
            public const string NotificationPage = "Notification";
            public const string Notification_View = "Notification.View";
            public const string Notification_Create = "Notification.Create";
            public const string Notification_Edit = "Notification.Edit";
            public const string Notification_Delete = "Notification.Delete";
        }

        public static class QuestionCategory
        {
            public const string QuestionCategoryPage = "QuestionCategory";
            public const string QuestionCategory_View = "QuestionCategory.View";
            public const string QuestionCategory_Create = "QuestionCategory.Create";
            public const string QuestionCategory_Edit = "QuestionCategory.Edit";
            public const string QuestionCategory_Delete = "QuestionCategory.Delete";
        }

        public static class Question
        {
            public const string QuestionPage = "Question";
            public const string Question_View = "Question.View";
            public const string Question_Create = "Question.Create";
            public const string Question_Edit = "Question.Edit";
            public const string Question_Delete = "Question.Delete";
        }

        public static class QuestionAnswer
        {
            public const string QuestionAnswerPage = "QuestionAnswer";
            public const string QuestionAnswer_View = "QuestionAnswer.View";
            public const string QuestionAnswer_Create = "QuestionAnswer.Create";
            public const string QuestionAnswer_Edit = "QuestionAnswer.Edit";
            public const string QuestionAnswer_Delete = "QuestionAnswer.Delete";
        }

        public static class Exam
        {
            public const string ExamPage = "Exam";
            public const string Exam_View = "Exam.View";
            public const string Exam_Create = "Exam.Create";
            public const string Exam_Edit = "Exam.Edit";
            public const string Exam_Delete = "Exam.Delete";
        }

        public static class ExamQuestion
        {
            public const string ExamQuestionPage = "ExamQuestion";
            public const string ExamQuestion_View = "ExamQuestion.View";
            public const string ExamQuestion_Create = "ExamQuestion.Create";
            public const string ExamQuestion_Edit = "ExamQuestion.Edit";
            public const string ExamQuestion_Delete = "ExamQuestion.Delete";
        }

        public static class ExamAttempt
        {
            public const string ExamAttemptPage = "ExamAttempt";
            public const string ExamAttempt_View = "ExamAttempt.View";
            public const string ExamAttempt_Create = "ExamAttempt.Create";
            public const string ExamAttempt_Edit = "ExamAttempt.Edit";
            public const string ExamAttempt_Delete = "ExamAttempt.Delete";
        }

        public static class ExamAnswer
        {
            public const string ExamAnswerPage = "ExamAnswer";
            public const string ExamAnswer_View = "ExamAnswer.View";
            public const string ExamAnswer_Create = "ExamAnswer.Create";
            public const string ExamAnswer_Edit = "ExamAnswer.Edit";
            public const string ExamAnswer_Delete = "ExamAnswer.Delete";
        }

        public static class ExamSchedule
        {
            public const string ExamSchedulePage = "ExamSchedule";
            public const string ExamSchedule_View = "ExamSchedule.View";
            public const string ExamSchedule_Create = "ExamSchedule.Create";
            public const string ExamSchedule_Edit = "ExamSchedule.Edit";
            public const string ExamSchedule_Delete = "ExamSchedule.Delete";
        }

        public static class ExamStudent
        {
            public const string ExamStudentPage = "ExamStudent";
            public const string ExamStudent_View = "ExamStudent.View";
            public const string ExamStudent_Create = "ExamStudent.Create";
            public const string ExamStudent_Edit = "ExamStudent.Edit";
            public const string ExamStudent_Delete = "ExamStudent.Delete";
        }

        public static class StudentGrade
        {
            public const string StudentGradePage = "StudentGrade";
            public const string StudentGrade_View = "StudentGrade.View";
            public const string StudentGrade_Create = "StudentGrade.Create";
            public const string StudentGrade_Edit = "StudentGrade.Edit";
            public const string StudentGrade_Delete = "StudentGrade.Delete";
            public const string StudentGrade_SaveGrade = "StudentGrade.SaveGrade";
        }

        public static class Homework
        {
            public const string HomeworkPage = "Homework";
            public const string Homework_View = "Homework.View";
            public const string Homework_Create = "Homework.Create";
            public const string Homework_Edit = "Homework.Edit";
            public const string Homework_Delete = "Homework.Delete";
        }

        public static class Semester
        {
            public const string SemesterPage = "Semester";
            public const string Semester_View = "Semester.View";
            public const string Semester_Create = "Semester.Create";
            public const string Semester_Edit = "Semester.Edit";
            public const string Semester_Delete = "Semester.Delete";
        }

        public static class StudentRegistration
        {
            public const string StudentRegistrationPage = "StudentRegistration";
            public const string StudentRegistration_View = "StudentRegistration.View";
            public const string StudentRegistration_Create = "StudentRegistration.Create";
            public const string StudentRegistration_Edit = "StudentRegistration.Edit";
            public const string StudentRegistration_Delete = "StudentRegistration.Delete";
            public const string StudentRegistration_Import = "StudentRegistration.Import";
        }

        public static List<string> GetAllPermissions()
        {
            var permissions = new List<string>();
            var nestedTypes = typeof(Permissions).GetNestedTypes(BindingFlags.Public);
            foreach (var type in nestedTypes)
            {
                var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                foreach (var field in fields)
                {
                    if (field.IsLiteral && !field.IsInitOnly && field.FieldType == typeof(string))
                    {
                        var value = field.GetRawConstantValue() as string;
                        if (value != null)
                        {
                            permissions.Add(value);
                        }
                    }
                }
            }
            return permissions;
        }

        public static readonly Dictionary<string, string> FeatureToGroupMapping = new()
        {
            { "Semester", "academicOperations" },
            { "Course", "academicOperations" },
            { "StudentRegistration", "academicOperations" },
            { "Class", "academicOperations" },
            { "StudentClass", "academicOperations" },
            { "Teacher", "academicOperations" },
            { "Student", "academicOperations" },
            { "Room", "academicOperations" },
            
            { "ClassSchedule", "schedule" },
            { "TimeSlot", "schedule" },
            
            { "Exam", "assessments" },
            { "ExamQuestion", "assessments" },
            { "ExamAttempt", "assessments" },
            { "ExamAnswer", "assessments" },
            { "ExamStudent", "assessments" },
            { "Homework", "assessments" },
            { "Question", "assessments" },
            { "QuestionCategory", "assessments" },
            { "StudentGrade", "assessments" },
            
            { "LearningMaterial", "learning" },
            { "Attendance", "learning" },
            
            { "User", "administration" },
            { "Role", "administration" },
            { "Notification", "administration" },
            
            { "ParentStudent", "parentServices" },
            
            { "Product", "others" }
        };

        public static Dictionary<string, Dictionary<string, List<string>>> GetStructuredPermissions()
        {
            var structured = new Dictionary<string, Dictionary<string, List<string>>>();
            
            var groups = new[] { "academicOperations", "schedule", "assessments", "learning", "administration", "parentServices", "others" };
            foreach (var g in groups)
            {
                structured[g] = new Dictionary<string, List<string>>();
            }

            var nestedTypes = typeof(Permissions).GetNestedTypes(BindingFlags.Public);
            foreach (var type in nestedTypes)
            {
                var featureName = type.Name;
                var groupId = FeatureToGroupMapping.GetValueOrDefault(featureName, "others");
                var groupNode = structured[groupId];

                var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                var permissionStrings = new List<string>();
                
                foreach (var field in fields)
                {
                    if (field.IsLiteral && !field.IsInitOnly && field.FieldType == typeof(string))
                    {
                        var value = field.GetRawConstantValue() as string;
                        if (value != null)
                        {
                            permissionStrings.Add(value);
                        }
                    }
                }

                if (permissionStrings.Count > 0)
                {
                    groupNode[featureName] = permissionStrings;
                }
            }

            // Remove empty groups
            var keys = structured.Keys.ToList();
            foreach (var key in keys)
            {
                if (structured[key].Count == 0)
                {
                    structured.Remove(key);
                }
            }

            return structured;
        }
    }
}

