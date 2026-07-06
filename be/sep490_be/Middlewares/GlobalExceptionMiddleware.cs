using System.Net;
using System.Text.Json;
using sep490_be.Helpers;

namespace sep490_be.Middlewares
{
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;

        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SERVER ERROR 500] Unhandled exception occurred while processing request: {Method} {Path}", 
                    context.Request.Method, context.Request.Path);
                    
                var queryString = context.Request.QueryString.HasValue ? context.Request.QueryString.Value : "None";
                var user = context.User?.Identity?.IsAuthenticated == true ? context.User.Identity.Name : "Anonymous";
                
                await FileLogger.LogErrorAsync(
                    "SERVER ERROR 500", 
                    context.Request.Method, 
                    context.Request.Path, 
                    $"Query: {queryString} | User: {user}\nException Message: {ex.Message}\nStackTrace: {ex.StackTrace}");
                
                await HandleExceptionAsync(context, ex);
            }
        }

        private static Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            var response = new
            {
                StatusCode = context.Response.StatusCode,
                Message = "An internal server error occurred.",
                Detailed = exception.Message // Keep this for easy debugging as requested
            };

            var json = JsonSerializer.Serialize(response);
            return context.Response.WriteAsync(json);
        }
    }
}

