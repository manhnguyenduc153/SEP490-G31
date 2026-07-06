namespace sep490_be.DTO
{
    public class BaseSearchDto
    {
        public string? Keyword { get; set; }
        public bool? Status { get; set; }
        public int PageIndex { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}

