namespace PRN232_be.DTO
{
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public int StatusCode { get; set; }
        public string? Message { get; set; }
        public T? Data { get; set; }

        public static ApiResponse<T> Ok(
            T data,
            string? message = null,
            int statusCode = StatusCodes.Status200OK)
        {
            return new ApiResponse<T>
            {
                Success = true,
                StatusCode = statusCode,
                Message = message,
                Data = data
            };
        }

        public static ApiResponse<T> Created(
            T data,
            string? message = null)
        {
            return new ApiResponse<T>
            {
                Success = true,
                StatusCode = StatusCodes.Status201Created,
                Message = message,
                Data = data
            };
        }

        public static ApiResponse<T> Fail(
            string message,
            int statusCode = StatusCodes.Status400BadRequest)
        {
            return new ApiResponse<T>
            {
                Success = false,
                StatusCode = statusCode,
                Message = message,
                Data = default
            };
        }
    }
}
