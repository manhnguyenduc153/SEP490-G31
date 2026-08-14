using System;
using sep490_be.Models;
using sep490_be.Repositories.Common;

namespace sep490_be.Repositories.Interfaces
{
    public interface IHomeworkRepository : IBaseRepository<Homework, ApplicationDbContext>
    {
        Task<bool> ClassExistsAsync(int classId);
        Task<bool> IsClassOpenForHomeworkAsync(int classId);
        Task<Student?> ResolveStudentByUsernameAsync(string username);
        Task<bool> IsStudentEnrolledInClassAsync(int studentId, int classId);
        Task<bool> IsStudentEnrolledInClassWithStatusAsync(int studentId, int classId, int[] statuses);
        Task<bool> TeacherExistsAsync(int teacherId);
        Task<bool> IsTeacherAssignedToClassAsync(int teacherId, int classId);
    }
}

