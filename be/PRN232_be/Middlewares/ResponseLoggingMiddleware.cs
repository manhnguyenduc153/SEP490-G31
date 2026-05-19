using PRN232_be.Helpers;

namespace PRN232_be.Middlewares
{
    public class ResponseLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ResponseLoggingMiddleware> _logger;

        public ResponseLoggingMiddleware(RequestDelegate next, ILogger<ResponseLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            await _next(context);

            if (context.Response.StatusCode >= 400 && context.Response.StatusCode < 500)
            {
                _logger.LogWarning("[CLIENT ERROR {StatusCode}] Client made a bad request: {Method} {Path}", 
                    context.Response.StatusCode, context.Request.Method, context.Request.Path);
                    
                var queryString = context.Request.QueryString.HasValue ? context.Request.QueryString.Value : "None";
                var user = context.User?.Identity?.IsAuthenticated == true ? context.User.Identity.Name : "Anonymous";
                
                await FileLogger.LogErrorAsync(
                    $"CLIENT ERROR {context.Response.StatusCode}", 
                    context.Request.Method, 
                    context.Request.Path, 
                    $"Query: {queryString} | User: {user}");
            }
        }
    }
}
