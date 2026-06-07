using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PRN232_be.DTO;
using PRN232_be.DTO.Product;
using PRN232_be.Models;
using PRN232_be.Repositories.Interfaces;
using PRN232_be.Services.Interfaces;
using PRN232_be.Helpers;
using Mapster;

namespace PRN232_be.Services.Implementations
{
    public class ProductService : IProductService
    {
        private readonly IProductRepository _productRepository;

        public ProductService(IProductRepository productRepository)
        {
            _productRepository = productRepository;
        }

        public async Task<ApiResponse<PagingResponse<ProductDto>>> GetAllProductsAsync(ProductSearchDto searchDto)
        {
            try
            {
                var query = _productRepository.FindAll();

                if (!string.IsNullOrWhiteSpace(searchDto.Keyword))
                {
                    query = query.Where(p => p.TextSearch != null && p.TextSearch.Contains(searchDto.Keyword));
                }

                if (searchDto.MinPrice.HasValue)
                {
                    query = query.Where(p => p.Price >= searchDto.MinPrice.Value);
                }

                if (searchDto.MaxPrice.HasValue)
                {
                    query = query.Where(p => p.Price <= searchDto.MaxPrice.Value);
                }

                var dtoQuery = query.ProjectToType<ProductDto>();
                var pagingResponse = await dtoQuery.CreatePagingResponseAsync(searchDto);

                return ApiResponse<PagingResponse<ProductDto>>.Ok(pagingResponse, "Lấy danh sách thành công");
            }
            catch (Exception ex)
            {
                return ApiResponse<PagingResponse<ProductDto>>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<ProductDto>> GetProductByIdAsync(int id)
        {
            try
            {
                var product = await _productRepository.GetByIdAsync(id);
                if (product == null)
                {
                    return ApiResponse<ProductDto>.Fail("Không tìm thấy sản phẩm", StatusCodes.Status404NotFound);
                }

                var resultDto = product.Adapt<ProductDto>();
                return ApiResponse<ProductDto>.Ok(resultDto, "Lấy chi tiết thành công");
            }
            catch (Exception ex)
            {
                return ApiResponse<ProductDto>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<ProductDto>> CreateProductAsync(ProductSaveDto productDto)
        {
            try
            {
                var validationError = await ValidateProductAsync(productDto, isEdit: false);
                if (validationError != null)
                {
                    return ApiResponse<ProductDto>.Fail(validationError, StatusCodes.Status400BadRequest);
                }

                var product = productDto.Adapt<Product>();
                
                // Explicitly set Id to 0 so the database auto-increments it
                product.Id = 0; 
                
                await _productRepository.AddAsync(product);
                await _productRepository.SaveChangesAsync();
                
                var resultDto = product.Adapt<ProductDto>();
                return ApiResponse<ProductDto>.Created(resultDto, "Tạo mới thành công");
            }
            catch (Exception ex)
            {
                return ApiResponse<ProductDto>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<ProductDto>> EditProductAsync(ProductSaveDto productDto)
        {
            try
            {
                var validationError = await ValidateProductAsync(productDto, isEdit: true);
                if (validationError != null)
                {
                    return ApiResponse<ProductDto>.Fail(validationError, StatusCodes.Status400BadRequest);
                }

                var existingProduct = await _productRepository.GetByIdAsync(productDto.Id);
                if (existingProduct == null)
                {
                    return ApiResponse<ProductDto>.Fail("Không tìm thấy sản phẩm", StatusCodes.Status404NotFound);
                }

                productDto.Adapt(existingProduct);

                await _productRepository.UpdateAsync(existingProduct);
                await _productRepository.SaveChangesAsync();

                var resultDto = existingProduct.Adapt<ProductDto>();
                return ApiResponse<ProductDto>.Ok(resultDto, "Cập nhật thành công");
            }
            catch (Exception ex)
            {
                return ApiResponse<ProductDto>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        private async Task<string?> ValidateProductAsync(ProductSaveDto productDto, bool isEdit)
        {
            if (string.IsNullOrWhiteSpace(productDto.Code))
                return "Mã sản phẩm không được để trống";

            if (productDto.Code.Length > 50)
                return "Mã sản phẩm không được vượt quá 50 ký tự";

            if (string.IsNullOrWhiteSpace(productDto.Name))
                return "Tên sản phẩm không được để trống";

            if (productDto.Name.Length > 200)
                return "Tên sản phẩm không được vượt quá 200 ký tự";

            if (productDto.Price < 0)
                return "Giá sản phẩm không được nhỏ hơn 0";

            if (productDto.StockQuantity < 0)
                return "Số lượng tồn kho không được nhỏ hơn 0";

            // Kiểm tra trùng mã sản phẩm
            var duplicateProduct = await _productRepository.FindAll()
                .FirstOrDefaultAsync(p => p.Code == productDto.Code && (!isEdit || p.Id != productDto.Id));
            
            if (duplicateProduct != null)
            {
                return $"Mã sản phẩm '{productDto.Code}' đã tồn tại trên hệ thống";
            }

            return null; // Hợp lệ
        }

        public async Task<ApiResponse<bool>> DeleteProductAsync(int id)
        {
            try
            {
                var existingProduct = await _productRepository.GetByIdAsync(id);
                if (existingProduct == null)
                {
                    return ApiResponse<bool>.Fail("Không tìm thấy sản phẩm", StatusCodes.Status404NotFound);
                }

                await _productRepository.DeleteAsync(existingProduct);
                await _productRepository.SaveChangesAsync();

                return ApiResponse<bool>.Ok(true, "Xóa thành công");
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<bool>> DeactiveProductAsync(int id)
        {
            try
            {
                var existingProduct = await _productRepository.GetByIdAsync(id);
                if (existingProduct == null)
                {
                    return ApiResponse<bool>.Fail("Không tìm thấy sản phẩm", StatusCodes.Status404NotFound);
                }

                await _productRepository.DeactiveAsync(existingProduct);
                await _productRepository.SaveChangesAsync();

                return ApiResponse<bool>.Ok(true, "Vô hiệu hóa thành công");
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
            }
        }
    }
}
