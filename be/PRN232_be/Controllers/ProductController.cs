using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using PRN232_be.DTO.Product;
using PRN232_be.DTO.Common;
using PRN232_be.Services.Interfaces;
using PRN232_be.Helpers.Authorization;

namespace PRN232_be.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ProductController : ControllerBase
    {
        private readonly IProductService _productService;
        private readonly IAuthorizationService _authorizationService;

        public ProductController(IProductService productService, IAuthorizationService authorizationService)
        {
            _productService = productService;
            _authorizationService = authorizationService;
        }

        // GET: api/Product
        [HttpGet]
        [HasPermission(DmsPermissions.Product.Product_View)]
        public async Task<IActionResult> GetAllProducts([FromQuery] ProductSearchDto searchDto)
        {
            var response = await _productService.GetAllProductsAsync(searchDto);
            return StatusCode(response.StatusCode, response);
        }

        // GET: api/Product/5
        [HttpGet("{id}")]
        [HasPermission(DmsPermissions.Product.Product_View)]
        public async Task<IActionResult> GetProductById(int id)
        {
            var response = await _productService.GetProductByIdAsync(id);
            return StatusCode(response.StatusCode, response);
        }

        // POST: api/Product/CreateOrEdit
        [HttpPost("CreateOrEdit")]
        public async Task<IActionResult> CreateOrEditProduct([FromBody] CreateOrEditProductDto productDto)
        {
            if (productDto.Id == 0)
            {
                var authResult = await _authorizationService.AuthorizeAsync(User, DmsPermissions.Product.Product_Create);
                if (!authResult.Succeeded)
                {
                    return Forbid();
                }
            }
            else
            {
                var authResult = await _authorizationService.AuthorizeAsync(User, DmsPermissions.Product.Product_Edit);
                if (!authResult.Succeeded)
                {
                    return Forbid();
                }
            }

            var response = await _productService.CreateOrEditProductAsync(productDto);
            return StatusCode(response.StatusCode, response);
        }

        // DELETE: api/Product/5
        [HttpDelete("{id}")]
        [HasPermission(DmsPermissions.Product.Product_Delete)]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var response = await _productService.DeleteProductAsync(id);
            return StatusCode(response.StatusCode, response);
        }
    }
}
