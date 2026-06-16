using Microsoft.EntityFrameworkCore;
using PRN232_be.DTO;

namespace PRN232_be.Helpers
{
    public static class PagingHelper
    {
        /// <summary>
        /// Auto paging + mapping dùng Mapster/ProjectToType (IQueryable → DTO).
        /// Dùng khi DTO không cần logic tính toán thêm.
        /// </summary>
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

        /// <summary>
        /// Manual paging dùng khi cần map thủ công (entity → DTO) phía C#.
        /// Bước 1: đếm + phân trang trên IQueryable&lt;TEntity&gt; ở DB level.
        /// Bước 2: ToListAsync rồi map qua IEnumerable&lt;TDto&gt; ở memory.
        /// </summary>
        public static PagingResponse<TDto> ToPagingResponse<TDto>(
            this IEnumerable<TDto> items,
            int totalRecords,
            BaseSearchDto searchDto)
        {
            return items.ToPagingResponse(totalRecords, searchDto.PageIndex, searchDto.PageSize);
        }

        public static PagingResponse<TDto> ToPagingResponse<TDto>(
            this IEnumerable<TDto> items,
            int totalRecords,
            int pageIndex,
            int pageSize)
        {
            return new PagingResponse<TDto>
            {
                PageIndex = pageIndex,
                PageSize = pageSize,
                TotalRecords = totalRecords,
                Items = items
            };
        }

        /// <summary>
        /// Áp dụng Skip/Take theo searchDto rồi ToListAsync.
        /// Dùng kết hợp với ToPagingResponse() khi map thủ công:
        /// <code>
        /// var total = await query.CountAsync();
        /// var items = await query.ApplyPagingAsync(searchDto);
        /// var dtos  = items.Select(MapToDto);
        /// return dtos.ToPagingResponse(total, searchDto);
        /// </code>
        /// </summary>
        public static Task<List<T>> ApplyPagingAsync<T>(
            this IQueryable<T> query,
            BaseSearchDto searchDto) where T : class
        {
            return query.ApplyPagingAsync(searchDto.PageIndex, searchDto.PageSize);
        }

        public static Task<List<T>> ApplyPagingAsync<T>(
            this IQueryable<T> query,
            int pageIndex,
            int pageSize) where T : class
        {
            return query
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }
    }
}
