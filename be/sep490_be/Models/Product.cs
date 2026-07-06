using sep490_be.Models.BaseEntities;

namespace sep490_be.Models
{
    public class Product : StandardEntity<int>
    {
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int StockQuantity { get; set; }
    }
}

