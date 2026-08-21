using System.Threading.Tasks;
using sep490_be.Models;

namespace sep490_be.Services.Interfaces
{
    public interface INotificationService
    {
        Task SendClassCreatedNotificationAsync(Class classEntity);
        Task SendClassStatusChangedNotificationAsync(Class classEntity, int oldStatus, int newStatus);
        Task SendStudentsAddedToClassNotificationAsync(Class classEntity, List<int> newStudentIds);
        Task SendTeacherAssignedToClassNotificationAsync(Class classEntity, int teacherId);
        Task SendExamCreatedNotificationAsync(Exam examEntity);
        Task SendExamPublishedNotificationAsync(Exam examEntity);
        Task SendHomeworkCreatedNotificationAsync(Homework homeworkEntity);
    }
}
