using PRN232_be.Models;
using PRN232_be.Repositories.Common;
using PRN232_be.Repositories.Interfaces;

namespace PRN232_be.Repositories.Implementations
{
    public class ProductRepository : BaseRepository<Product, ApplicationDbContext>, IProductRepository
    {
        public ProductRepository(ApplicationDbContext context, IUnitOfWork unitOfWork) : base(context, unitOfWork)
        {
        }
    }
}
