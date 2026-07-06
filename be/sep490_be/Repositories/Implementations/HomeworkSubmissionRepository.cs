using System;
using sep490_be.Models;
using sep490_be.Repositories.Interfaces;
using sep490_be.Repositories.Common;

namespace sep490_be.Repositories.Implementations
{
    public class HomeworkSubmissionRepository : BaseRepository<HomeworkSubmission, ApplicationDbContext>, IHomeworkSubmissionRepository
    {
        public HomeworkSubmissionRepository(ApplicationDbContext context, IUnitOfWork unitOfWork) : base(context, unitOfWork)
        {
        }
    }
}

