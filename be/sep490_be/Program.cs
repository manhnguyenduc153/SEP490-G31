using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using System.Text.Json.Serialization;
using sep490_be.Extensions;
using sep490_be.Middlewares;
using sep490_be.Models;

var builder = WebApplication.CreateBuilder(args);

// 1. Thêm các dịch vụ Web (CORS, Controllers, JSON Options, Swagger)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:3000", "https://sep-490-g31-fe.vercel.app") // Port 5173 và 3000 của React và Domain Vercel
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddControllers().AddJsonOptions(x =>
{
    x.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    x.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    x.JsonSerializerOptions.Converters.Add(new sep490_be.Helpers.DateTimeJsonConverter());
    x.JsonSerializerOptions.Converters.Add(new sep490_be.Helpers.NullableDateTimeJsonConverter());
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Cấu hình để Swagger biết cách nhập Token
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Nhập token vào ô bên dưới"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// 2. Thêm các dịch vụ Infrastructure (Database, Redis)
builder.Services.AddInfrastructureServices(builder.Configuration);

// 3. Thêm các dịch vụ bảo mật (Authentication, Authorization)
builder.Services.AddSecurityServices(builder.Configuration);

// 4. Thêm Rate Limiting
builder.Services.AddRateLimitingServices(builder.Configuration);

// 5. Đăng ký các Dependency Injection (DI) riêng của dự án
builder.Services.RegisterCustomServices();

var app = builder.Build();

// Auto apply migrations & Seed Identity Data
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
        await sep490_be.Helpers.IdentityDataSeeder.SeedDataAsync(services);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating or seeding the database.");
    }
}

// Configure the HTTP request pipeline.
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseMiddleware<ResponseLoggingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI();

app.UseRouting();

app.UseStaticFiles();

app.UseCors("AllowReactApp");

// Rate Limiting middleware (đặt sau CORS, trước Authentication)
app.UseRateLimiter();

// app.UseHttpsRedirection(); // Có thể mở lại nếu bạn dùng HTTPS

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers().RequireRateLimiting("fixed");

app.MapHub<sep490_be.Hubs.NotificationHub>("/hubs/notification");

app.MapGet("/server123", () =>
{
    var name = System.Net.Dns.GetHostName();
    var ip = System.Net.Dns.GetHostAddresses(name)
        .First(x => x.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        .ToString();

    return new { name, ip };
});

app.Run();

public partial class Program { }

