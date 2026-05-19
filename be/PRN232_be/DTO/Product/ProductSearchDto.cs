namespace PRN232_be.DTO.Product
{
    public class ProductSearchDto : BaseSearchDto
    {
        // Add specific search fields for Product if needed, for example:
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
    }
}
