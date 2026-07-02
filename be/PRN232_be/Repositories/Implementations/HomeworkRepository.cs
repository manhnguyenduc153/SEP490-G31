using System;
using PRN232_be.Models;
using PRN232_be.Repositories.Interfaces;
using PRN232_be.Repositories.Common;

namespace PRN232_be.Repositories.Implementations
{
    public class HomeworkRepository : BaseRepository<Homework, ApplicationDbContext>, IHomeworkRepository
    {
        public HomeworkRepository(ApplicationDbContext context, IUnitOfWork unitOfWork) : base(context, unitOfWork)
        {
        }
    }
}
