using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using sep490_be.Models.BaseEntities;
using sep490_be.Repositories.Common;

namespace sep490_be.Helpers
{
    public static class ValidationHelper
    {
        public static async Task<(bool codeExists, bool nameExists)> CheckDuplicateCodeAndNameAsync<T, TContext>(
            IBaseRepository<T, TContext> repository,
            int? currentId,
            string code,
            string name)
            where T : StandardEntity<int>
            where TContext : DbContext
        {
            string trimmedCode = code.Trim().ToLower();
            string trimmedName = name.Trim().ToLower();

            bool codeExists = await repository.ExistsAsync(x =>
                x.Code.ToLower() == trimmedCode &&
                !x.IsDeleted &&
                (currentId == null || x.Id != currentId.Value));

            bool nameExists = await repository.ExistsAsync(x =>
                x.Name.ToLower() == trimmedName &&
                !x.IsDeleted &&
                (currentId == null || x.Id != currentId.Value));

            return (codeExists, nameExists);
        }
    }
}
