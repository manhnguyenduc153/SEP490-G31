using sep490_be.DTO;
using sep490_be.DTO.Product;

namespace sep490_be.Services.Interfaces
{
    public interface IProductService
    {
        Task<ApiResponse<PagingResponse<ProductDto>>> GetAllProductsAsync(ProductSearchDto searchDto);
        Task<ApiResponse<ProductDto>> GetProductByIdAsync(int id);
        Task<ApiResponse<ProductDto>> CreateProductAsync(ProductSaveDto productDto);
        Task<ApiResponse<ProductDto>> EditProductAsync(ProductSaveDto productDto);
        Task<ApiResponse<bool>> DeleteProductAsync(int id);
        Task<ApiResponse<bool>> DeactiveProductAsync(int id);
    }
}

