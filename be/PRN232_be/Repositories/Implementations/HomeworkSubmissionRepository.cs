using System;
using PRN232_be.Models;
using PRN232_be.Repositories.Interfaces;
using PRN232_be.Repositories.Common;

namespace PRN232_be.Repositories.Implementations
{
    public class HomeworkSubmissionRepository : BaseRepository<HomeworkSubmission, ApplicationDbContext>, IHomeworkSubmissionRepository
    {
        public HomeworkSubmissionRepository(ApplicationDbContext context, IUnitOfWork unitOfWork) : base(context, unitOfWork)
        {
        }
    }
}
