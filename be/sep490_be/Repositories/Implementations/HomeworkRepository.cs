using System;
using sep490_be.Models;
using sep490_be.Repositories.Interfaces;
using sep490_be.Repositories.Common;

namespace sep490_be.Repositories.Implementations
{
    public class HomeworkRepository : BaseRepository<Homework, ApplicationDbContext>, IHomeworkRepository
    {
        public HomeworkRepository(ApplicationDbContext context, IUnitOfWork unitOfWork) : base(context, unitOfWork)
        {
        }
    }
}

