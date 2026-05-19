using PRN232_be.DTO;
using PRN232_be.DTO.Product;

namespace PRN232_be.Services.Interfaces
{
    public interface IProductService
    {
        Task<ApiResponse<PagingResponse<ProductDto>>> GetAllProductsAsync(ProductSearchDto searchDto);
        Task<ApiResponse<ProductDto>> GetProductByIdAsync(int id);
        Task<ApiResponse<ProductDto>> CreateOrEditProductAsync(CreateOrEditProductDto productDto);
        Task<ApiResponse<bool>> DeleteProductAsync(int id);
    }
}
