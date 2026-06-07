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

        // POST: api/Product
        [HttpPost]
        [HasPermission(DmsPermissions.Product.Product_Create)]
        public async Task<IActionResult> CreateProduct([FromBody] ProductSaveDto productDto)
        {
            var response = await _productService.CreateProductAsync(productDto);
            return StatusCode(response.StatusCode, response);
        }

        // PUT: api/Product/{id}
        [HttpPut("{id}")]
        [HasPermission(DmsPermissions.Product.Product_Edit)]
        public async Task<IActionResult> EditProduct(int id, [FromBody] ProductSaveDto productDto)
        {
            productDto.Id = id;
            var response = await _productService.EditProductAsync(productDto);
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
