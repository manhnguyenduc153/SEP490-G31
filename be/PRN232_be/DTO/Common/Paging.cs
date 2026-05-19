namespace PRN232_be.DTO
{
    public class PagingResponse<T>
    {
        public int PageIndex { get; set; }
        public int PageSize { get; set; }
        public int TotalRecords { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalRecords / PageSize);
        public IEnumerable<T> Items { get; set; } = new List<T>();
    }
}
