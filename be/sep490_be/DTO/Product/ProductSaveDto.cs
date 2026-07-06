using sep490_be.Helpers;

namespace sep490_be.DTO.Product
{
    public class ProductSaveDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int StockQuantity { get; set; }

        public string TextSearch => StringHelper.GenerateTextSearch(Code, Name);
    }
}

