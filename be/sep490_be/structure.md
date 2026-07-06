# sep490_be — Backend Structure & Coding Convention

> Tài liệu này mô tả **kiến trúc tổng thể** của backend và các **quy ước code bắt buộc** khi thêm tính năng mới.
> Mọi tính năng mới **phải tuân theo** cấu trúc này để đảm bảo nhất quán.

---

## 📁 Cấu trúc thư mục

```
sep490_be/
├── Controllers/                  # API endpoints
├── DTO/
│   ├── Common/                   # Dùng chung: ApiResponse, PagingResponse, BaseSearchDto, Permissions
│   ├── Auth/                     # DTOs cho Authentication
│   ├── Product/                  # ProductDto, ProductSaveDto, ProductSearchDto
│   ├── Teacher/                  # TeacherDto, TeacherSaveDto, TeacherSearchDto
│   ├── QuestionCategory/         # ...
│   └── [Entity]/                 # Mỗi entity có 1 thư mục riêng
├── Enums/
│   ├── Enums.cs                  # Tất cả enum của hệ thống (StudentStatus, GeneralStatus,...)
│   └── ...
├── Extensions/
│   ├── ServicesRegister.cs       # ⭐ Đăng ký DI cho Repository & Service mới
│   ├── StartupExtensions.cs      # Cấu hình Infrastructure, Security, RateLimiting
│   └── ClaimsPrincipalExtensions.cs
├── Helpers/
│   ├── Authorization/
│   │   ├── HasPermissionAttribute.cs    # [HasPermission("...")] dùng trong Controller
│   │   ├── PermissionHandler.cs
│   │   ├── PermissionPolicyProvider.cs
│   │   └── PermissionRequirement.cs
│   ├── PagingHelper.cs           # Extension methods: CreatePagingResponseAsync, ToPagingResponse, ApplyPagingAsync
│   ├── StringHelper.cs           # GenerateTextSearch() cho TextSearch field
│   └── IdentityDataSeeder.cs
├── Middlewares/
│   ├── GlobalExceptionMiddleware.cs
│   └── ResponseLoggingMiddleware.cs
├── Models/
│   ├── BaseEntities/
│   │   ├── BaseEntity.cs         # Id property
│   │   ├── AuditableEntity.cs    # + CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, IsDeleted, DeletedAt, DeletedBy
│   │   └── StandardEntity.cs     # + Code, Name, TextSearch
│   ├── Configurations/           # ⭐ EF Core Fluent API config (IEntityTypeConfiguration<T>)
│   │   ├── ProductConfiguration.cs
│   │   ├── RoomConfiguration.cs
│   │   └── [Entity]Configuration.cs
│   ├── ApplicationDbContext.cs   # ⭐ Thêm DbSet<T> khi có entity mới
│   ├── Room.cs
│   ├── Product.cs
│   └── [Entity].cs
├── Repositories/
│   ├── Common/
│   │   ├── IBaseRepository.cs    # Interface generic: CRUD, FindAll, Dapper, Transaction
│   │   ├── BaseRepository.cs     # Implementation generic
│   │   ├── IUnitOfWork.cs
│   │   └── UnitOfWork.cs
│   ├── Interfaces/
│   │   ├── IProductRepository.cs
│   │   └── I[Entity]Repository.cs
│   └── Implementations/
│       ├── ProductRepository.cs
│       └── [Entity]Repository.cs
├── Services/
│   ├── Interfaces/
│   │   ├── IProductService.cs
│   │   └── I[Entity]Service.cs
│   └── Implementations/
│       ├── ProductService.cs
│       └── [Entity]Service.cs
├── Program.cs                    # Entry point: gọi extension methods từ Extensions/
└── appsettings.json
```

---

## 🔁 Luồng xử lý chuẩn (Request Flow)

```
HTTP Request
    ↓
[Controller]  → kiểm tra [HasPermission], nhận DTO từ request
    ↓
[Service]     → business logic, validate, map DTO ↔ Entity (dùng Mapster)
    ↓
[Repository]  → gọi EF Core / Dapper trên ApplicationDbContext
    ↓
[Database]    → SQL Server
    ↓
[Service]     → wrap kết quả vào ApiResponse<T>
    ↓
[Controller]  → return StatusCode(response.StatusCode, response)
```

---

## 📐 Quy ước đặt tên

| Layer | Naming Convention | Ví dụ |
|---|---|---|
| Entity | `PascalCase`, đơn số | `Room`, `ClassSchedule` |
| DbSet | `PascalCase`, số nhiều | `Rooms`, `ClassSchedules` |
| Repository Interface | `I[Entity]Repository` | `IRoomRepository` |
| Repository Implementation | `[Entity]Repository` | `RoomRepository` |
| Service Interface | `I[Entity]Service` | `IRoomService` |
| Service Implementation | `[Entity]Service` | `RoomService` |
| Controller | `[Entity]Controller` | `RoomController` |
| DTO (read) | `[Entity]Dto` | `RoomDto` |
| DTO (create/edit) | `[Entity]SaveDto` | `RoomSaveDto` |
| DTO (search/filter) | `[Entity]SearchDto` | `RoomSearchDto` |
| EF Config | `[Entity]Configuration` | `RoomConfiguration` |

---

## 🗄️ Entity Model Convention

Tất cả entity đều kế thừa từ một trong các base class sau:

```csharp
// Cấp 1: Chỉ có Id
public abstract class BaseEntity<TKey> { public TKey Id { get; set; } }

// Cấp 2: + Audit fields (CreatedAt, CreatedBy, UpdatedAt, IsDeleted, ...)
public abstract class AuditableEntity<TKey> : BaseEntity<TKey> { ... }

// Cấp 3: + Code, Name, TextSearch (dùng cho các entity có tên & mã)
public abstract class StandardEntity<TKey> : AuditableEntity<TKey> { ... }
```

**Hầu hết entity dùng `StandardEntity<int>`:**
```csharp
public class Room : StandardEntity<int>
{
    public int? Capacity { get; set; }
    public int Status { get; set; }  // Dùng enum tương ứng (GeneralStatus, ...)

    // Navigation properties
    public virtual ICollection<ClassSchedule> ClassSchedules { get; set; } = new List<ClassSchedule>();
}
```

> ⚠️ `IsDeleted` được tự động set khi gọi `SaveChanges`. Không bao giờ set thủ công ngoài DbContext.

---

## 🗃️ EF Core Configuration Convention

Mỗi entity phải có file `[Entity]Configuration.cs` trong `Models/Configurations/`:

```csharp
public class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.ToTable("rooms");              // Tên bảng snake_case hoặc PascalCase (theo convention hiện tại)
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);

        // ⭐ Soft-delete global filter — BẮT BUỘC có ở mọi entity
        builder.HasQueryFilter(x => !x.IsDeleted);

        // Navigation / FK relations (nếu có)
        builder.HasOne(x => x.SomeEntity)
               .WithMany(e => e.Rooms)
               .HasForeignKey(x => x.SomeEntityId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
```

> ApplicationDbContext tự động load tất cả config qua `modelBuilder.ApplyConfigurationsFromAssembly(...)` — **không cần đăng ký thủ công**.

---

## 📦 Repository Convention

### Interface
```csharp
// Repositories/Interfaces/IRoomRepository.cs
using sep490_be.Models;
using sep490_be.Repositories.Common;

namespace sep490_be.Repositories.Interfaces
{
    public interface IRoomRepository : IBaseRepository<Room, ApplicationDbContext>
    {
        // Chỉ thêm method đặc thù nếu cần, còn lại kế thừa từ IBaseRepository
    }
}
```

### Implementation
```csharp
// Repositories/Implementations/RoomRepository.cs
using sep490_be.Models;
using sep490_be.Repositories.Common;
using sep490_be.Repositories.Interfaces;

namespace sep490_be.Repositories.Implementations
{
    public class RoomRepository : BaseRepository<Room, ApplicationDbContext>, IRoomRepository
    {
        public RoomRepository(ApplicationDbContext context, IUnitOfWork unitOfWork)
            : base(context, unitOfWork) { }
    }
}
```

### IBaseRepository — các method sẵn có
```csharp
// EF Core CRUD
Task<T?> GetByIdAsync(int id);
Task<T> AddAsync(T entity);
Task UpdateAsync(T entity);
Task DeactiveAsync(T entity);   // soft-delete: set IsDeleted = true
Task DeleteAsync(T entity);     // hard delete (ApplicationDbContext intercepts → soft delete)
Task<int> SaveChangesAsync();

// Query
IQueryable<T> FindAll(bool trackChanges = false);
IQueryable<T> FindByCondition(Expression<Func<T, bool>> expr, bool trackChanges = false);
Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate);

// Dapper (raw SQL)
Task<IEnumerable<TResult>> DapperQueryAsync<TResult>(string sql, object? param = null);
Task<TResult?> DapperGetAsync<TResult>(string sql, object? param = null);
Task<int> DapperExecuteAsync(string sql, object? param = null);

// Transaction
Task<IDbContextTransaction> BeginTransactionAsync();
Task CommitTransactionAsync();
Task RollbackTransactionAsync();
```

---

## 🔧 Service Convention

### Interface
```csharp
// Services/Interfaces/IRoomService.cs
using sep490_be.DTO;
using sep490_be.DTO.Room;

namespace sep490_be.Services.Interfaces
{
    public interface IRoomService
    {
        Task<ApiResponse<PagingResponse<RoomDto>>> GetAllRoomsAsync(RoomSearchDto searchDto);
        Task<ApiResponse<RoomDto>> GetRoomByIdAsync(int id);
        Task<ApiResponse<RoomDto>> CreateRoomAsync(RoomSaveDto dto);
        Task<ApiResponse<RoomDto>> EditRoomAsync(RoomSaveDto dto);
        Task<ApiResponse<bool>> DeleteRoomAsync(int id);
        Task<ApiResponse<bool>> DeactiveRoomAsync(int id);
    }
}
```

### Implementation — Template
```csharp
// Services/Implementations/RoomService.cs
public class RoomService : IRoomService
{
    private readonly IRoomRepository _roomRepository;

    public RoomService(IRoomRepository roomRepository) { _roomRepository = roomRepository; }

    public async Task<ApiResponse<PagingResponse<RoomDto>>> GetAllRoomsAsync(RoomSearchDto searchDto)
    {
        try
        {
            var query = _roomRepository.FindAll();

            // Filter by keyword (TextSearch)
            if (!string.IsNullOrWhiteSpace(searchDto.Keyword))
                query = query.Where(x => x.TextSearch != null && x.TextSearch.Contains(searchDto.Keyword));

            // Filter by status (nếu entity có Status)
            // if (searchDto.Status.HasValue)
            //     query = query.Where(x => x.Status == (int)(searchDto.Status.Value ? GeneralStatus.Active : GeneralStatus.Inactive));

            // Dùng Mapster ProjectToType + PagingHelper
            var dtoQuery = query.ProjectToType<RoomDto>();
            var result = await dtoQuery.CreatePagingResponseAsync(searchDto);

            return ApiResponse<PagingResponse<RoomDto>>.Ok(result, "Lấy danh sách thành công");
        }
        catch (Exception ex)
        {
            return ApiResponse<PagingResponse<RoomDto>>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
        }
    }

    public async Task<ApiResponse<RoomDto>> GetRoomByIdAsync(int id)
    {
        try
        {
            var entity = await _roomRepository.GetByIdAsync(id);
            if (entity == null)
                return ApiResponse<RoomDto>.Fail("Không tìm thấy phòng học", StatusCodes.Status404NotFound);

            return ApiResponse<RoomDto>.Ok(entity.Adapt<RoomDto>(), "Lấy chi tiết thành công");
        }
        catch (Exception ex)
        {
            return ApiResponse<RoomDto>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
        }
    }

    public async Task<ApiResponse<RoomDto>> CreateRoomAsync(RoomSaveDto dto)
    {
        try
        {
            var validationError = await ValidateAsync(dto, isEdit: false);
            if (validationError != null)
                return ApiResponse<RoomDto>.Fail(validationError, StatusCodes.Status400BadRequest);

            var entity = dto.Adapt<Room>();
            entity.Id = 0;  // Đảm bảo auto-increment
            await _roomRepository.AddAsync(entity);
            await _roomRepository.SaveChangesAsync();

            return ApiResponse<RoomDto>.Created(entity.Adapt<RoomDto>(), "Tạo mới thành công");
        }
        catch (Exception ex)
        {
            return ApiResponse<RoomDto>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
        }
    }

    public async Task<ApiResponse<RoomDto>> EditRoomAsync(RoomSaveDto dto)
    {
        try
        {
            var validationError = await ValidateAsync(dto, isEdit: true);
            if (validationError != null)
                return ApiResponse<RoomDto>.Fail(validationError, StatusCodes.Status400BadRequest);

            var entity = await _roomRepository.GetByIdAsync(dto.Id);
            if (entity == null)
                return ApiResponse<RoomDto>.Fail("Không tìm thấy phòng học", StatusCodes.Status404NotFound);

            dto.Adapt(entity);  // Map DTO vào entity (Mapster)
            await _roomRepository.UpdateAsync(entity);
            await _roomRepository.SaveChangesAsync();

            return ApiResponse<RoomDto>.Ok(entity.Adapt<RoomDto>(), "Cập nhật thành công");
        }
        catch (Exception ex)
        {
            return ApiResponse<RoomDto>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
        }
    }

    public async Task<ApiResponse<bool>> DeleteRoomAsync(int id)
    {
        try
        {
            var entity = await _roomRepository.GetByIdAsync(id);
            if (entity == null)
                return ApiResponse<bool>.Fail("Không tìm thấy phòng học", StatusCodes.Status404NotFound);

            await _roomRepository.DeleteAsync(entity);
            await _roomRepository.SaveChangesAsync();
            return ApiResponse<bool>.Ok(true, "Xóa thành công");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
        }
    }

    public async Task<ApiResponse<bool>> DeactiveRoomAsync(int id)
    {
        try
        {
            var entity = await _roomRepository.GetByIdAsync(id);
            if (entity == null)
                return ApiResponse<bool>.Fail("Không tìm thấy phòng học", StatusCodes.Status404NotFound);

            await _roomRepository.DeactiveAsync(entity);
            await _roomRepository.SaveChangesAsync();
            return ApiResponse<bool>.Ok(true, "Vô hiệu hóa thành công");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail(ex.Message, StatusCodes.Status500InternalServerError);
        }
    }

    // Validation helper — dùng private method riêng trong mỗi service
    private async Task<string?> ValidateAsync(RoomSaveDto dto, bool isEdit)
    {
        if (string.IsNullOrWhiteSpace(dto.Code)) return "Mã phòng không được để trống";
        if (dto.Code.Length > 50) return "Mã phòng không vượt quá 50 ký tự";
        if (string.IsNullOrWhiteSpace(dto.Name)) return "Tên phòng không được để trống";

        var duplicate = await _roomRepository.FindAll()
            .FirstOrDefaultAsync(x => x.Code == dto.Code && (!isEdit || x.Id != dto.Id));
        if (duplicate != null) return $"Mã phòng '{dto.Code}' đã tồn tại";

        return null;
    }
}
```

---

## 🌐 Controller Convention

```csharp
// Controllers/RoomController.cs
[Route("api/[controller]")]
[ApiController]
[Authorize]
public class RoomController : ControllerBase
{
    private readonly IRoomService _roomService;

    public RoomController(IRoomService roomService) { _roomService = roomService; }

    [HttpGet]
    [HasPermission(Permissions.Room.Room_View)]
    public async Task<IActionResult> GetAll([FromQuery] RoomSearchDto searchDto)
    {
        var response = await _roomService.GetAllRoomsAsync(searchDto);
        return StatusCode(response.StatusCode, response);
    }

    [HttpGet("{id}")]
    [HasPermission(Permissions.Room.Room_View)]
    public async Task<IActionResult> GetById(int id)
    {
        var response = await _roomService.GetRoomByIdAsync(id);
        return StatusCode(response.StatusCode, response);
    }

    [HttpPost]
    [HasPermission(Permissions.Room.Room_Create)]
    public async Task<IActionResult> Create([FromBody] RoomSaveDto dto)
    {
        var response = await _roomService.CreateRoomAsync(dto);
        return StatusCode(response.StatusCode, response);
    }

    [HttpPut("{id}")]
    [HasPermission(Permissions.Room.Room_Edit)]
    public async Task<IActionResult> Edit(int id, [FromBody] RoomSaveDto dto)
    {
        dto.Id = id;
        var response = await _roomService.EditRoomAsync(dto);
        return StatusCode(response.StatusCode, response);
    }

    [HttpDelete("{id}")]
    [HasPermission(Permissions.Room.Room_Delete)]
    public async Task<IActionResult> Delete(int id)
    {
        var response = await _roomService.DeleteRoomAsync(id);
        return StatusCode(response.StatusCode, response);
    }

    [HttpPost("{id}/deactive")]
    [HasPermission(Permissions.Room.Room_Delete)]
    public async Task<IActionResult> Deactive(int id)
    {
        var response = await _roomService.DeactiveRoomAsync(id);
        return StatusCode(response.StatusCode, response);
    }
}
```

> ⭐ **Pattern trả về**: luôn dùng `return StatusCode(response.StatusCode, response)` — không dùng `Ok()`, `NotFound()`, etc. trực tiếp.

---

## 📋 DTO Convention

### [Entity]Dto — dùng để trả về (read)
```csharp
// DTO/Room/RoomDto.cs
namespace sep490_be.DTO.Room
{
    public class RoomDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int? Capacity { get; set; }
        public int Status { get; set; }
    }
}
```

### [Entity]SaveDto — dùng để tạo/sửa (write)
```csharp
// DTO/Room/RoomSaveDto.cs
using sep490_be.Helpers;

namespace sep490_be.DTO.Room
{
    public class RoomSaveDto
    {
        public int Id { get; set; }  // 0 khi tạo mới, > 0 khi cập nhật
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int? Capacity { get; set; }
        public int Status { get; set; }

        // ⭐ TextSearch tự generate — dùng để lưu vào DB cho full-text search
        public string TextSearch => StringHelper.GenerateTextSearch(Code, Name);
    }
}
```

### [Entity]SearchDto — dùng để filter/search ([FromQuery])
```csharp
// DTO/Room/RoomSearchDto.cs
namespace sep490_be.DTO.Room
{
    public class RoomSearchDto : BaseSearchDto
    {
        // Thêm filter đặc thù nếu cần, ví dụ:
        public int? MinCapacity { get; set; }
    }
}
```

> `BaseSearchDto` đã có sẵn: `Keyword`, `Status`, `PageIndex` (default 1), `PageSize` (default 10).

---

## 🔐 Permissions Convention

Tất cả permission được định nghĩa tập trung trong `DTO/Common/Permissions.cs`:

```csharp
public static class Permissions
{
    public static class Room
    {
        public const string RoomPage   = "Room";
        public const string Room_View   = "Room.View";
        public const string Room_Create = "Room.Create";
        public const string Room_Edit   = "Room.Edit";
        public const string Room_Delete = "Room.Delete";
    }
    // ... các module khác
}
```

Dùng trong Controller: `[HasPermission(Permissions.Room.Room_View)]`

---

## ⚙️ Đăng ký DI — ServicesRegister.cs

Sau khi tạo xong Repository và Service mới, **bắt buộc** đăng ký vào `Extensions/ServicesRegister.cs`:

```csharp
public static void RegisterCustomServices(this IServiceCollection services)
{
    // --- Repositories ---
    services.AddScoped<IUnitOfWork, UnitOfWork<ApplicationDbContext>>();
    services.AddScoped(typeof(IBaseRepository<,>), typeof(BaseRepository<,>));
    services.AddScoped<IProductRepository, ProductRepository>();
    services.AddScoped<IQuestionCategoryRepository, QuestionCategoryRepository>();
    services.AddScoped<ITeacherRepository, TeacherRepository>();
    services.AddScoped<IRoomRepository, RoomRepository>();      // ← Thêm tại đây
    // ...

    // --- Services ---
    services.AddScoped<IProductService, ProductService>();
    services.AddScoped<IQuestionCategoryService, QuestionCategoryService>();
    services.AddScoped<IAuthService, AuthService>();
    services.AddScoped<ITeacherService, TeacherService>();
    services.AddScoped<IFileService, FileService>();
    services.AddScoped<IRoomService, RoomService>();            // ← Thêm tại đây
    // ...
}
```

---

## 🗂️ Danh sách Entities hiện có (ApplicationDbContext)

| DbSet | Entity | Table | Kế thừa |
|---|---|---|---|
| `Products` | `Product` | `Products` | `StandardEntity<int>` |
| `Rooms` | `Room` | `rooms` | `StandardEntity<int>` |
| `TimeSlots` | `TimeSlot` | *(config)* | `StandardEntity<int>` |
| `Courses` | `Course` | *(config)* | `StandardEntity<int>` |
| `Classes` | `Class` | *(config)* | `StandardEntity<int>` |
| `Teachers` | `Teacher` | *(config)* | `StandardEntity<int>` |
| `Students` | `Student` | *(config)* | `StandardEntity<int>` |
| `ParentStudents` | `ParentStudent` | *(config)* | `AuditableEntity<int>` |
| `StudentClasses` | `StudentClass` | *(config)* | `AuditableEntity<int>` |
| `ClassSchedules` | `ClassSchedule` | `class_schedules` | `StandardEntity<int>` |
| `Attendances` | `Attendance` | *(config)* | `AuditableEntity<int>` |
| `LearningMaterials` | `LearningMaterial` | *(config)* | `StandardEntity<int>` |
| `Notifications` | `Notification` | *(config)* | `AuditableEntity<int>` |
| `QuestionCategories` | `QuestionCategory` | *(config)* | `StandardEntity<int>` |
| `Questions` | `Question` | *(config)* | `StandardEntity<int>` |
| `QuestionAnswers` | `QuestionAnswer` | *(config)* | `AuditableEntity<int>` |
| `Activities` | `Activity` | *(config)* | `StandardEntity<int>` |
| `ActivityQuestions` | `ActivityQuestion` | *(config)* | `AuditableEntity<int>` |
| `ActivityAttempts` | `ActivityAttempt` | *(config)* | `AuditableEntity<int>` |
| `ActivityAnswers` | `ActivityAnswer` | *(config)* | `AuditableEntity<int>` |
| `ExamSchedules` | `ExamSchedule` | *(config)* | `StandardEntity<int>` |
| `ExamStudents` | `ExamStudent` | *(config)* | `AuditableEntity<int>` |
| `StudentGrades` | `StudentGrade` | *(config)* | `AuditableEntity<int>` |
| `RefreshTokens` | `RefreshToken` | *(config)* | `BaseEntity<int>` |

---

## 🔑 Enums hiện có (Enums/Enums.cs)

| Enum | Values |
|---|---|
| `GeneralStatus` | `Inactive=0`, `Active=1` |
| `StudentStatus` | `Inactive=0`, `Active=1`, `Suspended=2`, `Graduated=3` |
| `TeacherStatus` | `Inactive=0`, `Active=1`, `OnLeave=2` |
| `ClassStatus` | `Planning=0`, `Active=1`, `Completed=2`, `Cancelled=3` |
| `StudentClassStatus` | `Enrolled=0`, `Studying=1`, `Completed=2`, `Dropped=3` |
| `ClassScheduleStatus` | `Scheduled=0`, `OnGoing=1`, `Completed=2`, `Cancelled=3` |
| `AttendanceStatus` | `Absent=0`, `Present=1`, `Late=2`, `Excused=3` |
| `NotificationStatus` | `Unread=0`, `Read=1` |
| `NotificationTargetType` | `All=1`, `Class=2`, `User=3` |
| `QuestionType` | `SingleChoice=1`, `MultipleChoice=2`, `Essay=3`, `TrueFalse=4` |
| `DifficultyLevel` | `Easy=1`, `Medium=2`, `Hard=3` |
| `ActivityType` | `Assignment=1`, `Quiz=2`, `Exam=3` |
| `ActivityStatus` | `Draft=0`, `Published=1`, `Closed=2` |
| `ActivityAttemptStatus` | `Incomplete=0`, `Submitted=1`, `Graded=2` |
| `ExamScheduleStatus` | `Scheduled=0`, `InProgress=1`, `Finished=2`, `Cancelled=3` |
| `ExamStudentStatus` | `Registered=0`, `Attended=1`, `Absent=2` |
| `GradeLevel` | `Foundation=1` → `Ielts65Plus=6` |

---

## ✅ Checklist khi thêm tính năng mới

- [ ] **Model**: Tạo `[Entity].cs` kế thừa đúng base class
- [ ] **Configuration**: Tạo `[Entity]Configuration.cs` với `HasQueryFilter(!x.IsDeleted)` và FK relations
- [ ] **DbContext**: Thêm `DbSet<Entity> Entities` vào `ApplicationDbContext.cs`
- [ ] **Migration**: Chạy `Add-Migration [TenMigration]` và `Update-Database`
- [ ] **DTO**: Tạo thư mục `DTO/[Entity]/` với 3 file: `[Entity]Dto`, `[Entity]SaveDto`, `[Entity]SearchDto`
- [ ] **Repository**: Tạo `I[Entity]Repository.cs` và `[Entity]Repository.cs`
- [ ] **Service**: Tạo `I[Entity]Service.cs` và `[Entity]Service.cs`
- [ ] **Controller**: Tạo `[Entity]Controller.cs` với đầy đủ endpoints
- [ ] **DI**: Đăng ký Repository và Service vào `ServicesRegister.cs`
- [ ] **Permissions**: Kiểm tra `Permissions.[Entity]` đã có trong `DTO/Common/Permissions.cs`

