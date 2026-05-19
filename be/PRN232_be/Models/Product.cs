using PRN232_be.Models.BaseEntities;

namespace PRN232_be.Models
{
    public class Product : StandardEntity<int>
    {
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int StockQuantity { get; set; }
    }
}
