//using PRN232_be.DTO;

//namespace PRN232_be.Background_Service
//{
//    public class CacheWarmupService : IHostedService
//    {
//        private readonly IServiceProvider _serviceProvider;
//        private readonly ILogger<CacheWarmupService> _logger;

//        // Số trang muốn warm up sẵn
//        private const int WarmupPages = 2;
//        private const int DefaultPageSize = 10;

//        public CacheWarmupService(IServiceProvider serviceProvider, ILogger<CacheWarmupService> logger)
//        {
//            _serviceProvider = serviceProvider;
//            _logger = logger;
//        }

//        public async Task StartAsync(CancellationToken cancellationToken)
//        {
//            _logger.LogInformation("[CacheWarmup] Starting cache warmup for first {Pages} pages of products...", WarmupPages);

//            try
//            {
//                // IProductService is Scoped, so we need to create a scope
//                using var scope = _serviceProvider.CreateScope();
//                var productService = scope.ServiceProvider.GetRequiredService<IProductService>();

//                for (int page = 1; page <= WarmupPages; page++)
//                {
//                    var searchDto = new ProductSearchDto
//                    {
//                        PageIndex = page,
//                        PageSize = DefaultPageSize
//                    };

//                    var result = await productService.GetProductsAsync(searchDto);

//                    if (result.Success)
//                    {
//                        _logger.LogInformation("[CacheWarmup] ✅ Warmed up page {Page}/{Total} - {Count} products cached",
//                            page, WarmupPages, result.Data?.Items.Count() ?? 0);
//                    }
//                    else
//                    {
//                        _logger.LogWarning("[CacheWarmup] ⚠️ Failed to warm page {Page}: {Error}", page, result.Message);
//                    }
//                }

//                _logger.LogInformation("[CacheWarmup] ✅ Cache warmup complete for {Pages} pages.", WarmupPages);
//            }
//            catch (Exception ex)
//            {
//                // Warmup failure should NOT crash the app
//                _logger.LogError(ex, "[CacheWarmup] ❌ Cache warmup failed. App will continue normally.");
//            }
//        }

//        public Task StopAsync(CancellationToken cancellationToken)
//        {
//            return Task.CompletedTask;
//        }
//    }
//}
