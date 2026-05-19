using Microsoft.EntityFrameworkCore;
using PRN232_be.DTO;

namespace PRN232_be.Helpers
{
    public static class PagingHelper
    {
        public static async Task<PagingResponse<T>> CreatePagingResponseAsync<T>(
            this IQueryable<T> query,
            BaseSearchDto searchDto) where T : class
        {
            var totalRecords = await query.CountAsync();

            var items = await query
                .Skip((searchDto.PageIndex - 1) * searchDto.PageSize)
                .Take(searchDto.PageSize)
                .ToListAsync();

            return new PagingResponse<T>
            {
                PageIndex = searchDto.PageIndex,
                PageSize = searchDto.PageSize,
                TotalRecords = totalRecords,
                Items = items
            };
        }
    }
}
