# Hướng Dẫn Phát Triển Chức Năng CRUD - Backend (ASP.NET Core)

Tài liệu này hướng dẫn chi tiết quy trình xây dựng phần Backend cho một luồng chức năng CRUD (Thêm, Đọc, Sửa, Vô hiệu hóa/Xóa) áp dụng mô hình **N-Tier/Clean Architecture** rút gọn, sử dụng **Repository Pattern** và **Unit of Work** kết hợp xác thực quyền hạn (Permission-based) và Bản địa hóa (Localization).

Dữ liệu mẫu đối chiếu trực tiếp với chức năng **Danh mục câu hỏi (QuestionCategory)**.

---

## I. Tổng Quan Cấu Trúc Backend (`DoAn/be/sep490_be`)

Thư mục Backend tổ chức các thành phần xử lý nghiệp vụ theo sơ đồ phân tách trách nhiệm sau:

```
sep490_be/
├── Controllers/                  # API Controllers tiếp nhận HTTP Request & phân quyền
│   └── QuestionCategoryController.cs
├── DTO/                          # Data Transfer Objects (chứa data validation & text search helper)
│   ├── Common/
│   │   ├── BaseSearchDto.cs      # Class cơ sở chứa PageIndex, PageSize, Keyword
│   │   └── Permissions.cs        # Định nghĩa tất cả mã phân quyền trong hệ thống
│   └── QuestionCategory/
│       ├── QuestionCategoryDto.cs
│       ├── QuestionCategorySaveDto.cs
│       └── QuestionCategorySearchDto.cs
├── Models/                       # Entities tương ứng với database & cấu hình EF Core
│   ├── BaseEntities/
│   │   ├── BaseEntity.cs         # Chứa khóa chính Id
│   │   ├── AuditableEntity.cs    # Chứa audit log (CreatedAt, CreatedBy, IsDeleted, DeletedAt,...)
│   │   └── StandardEntity.cs     # Chứa Code, Name, TextSearch
│   ├── Configurations/
│   │   └── QuestionCategoryConfiguration.cs # Cấu hình bảng, độ dài cột, soft-delete filter
│   ├── ApplicationDbContext.cs   # DbContext quản lý DbSets & cơ chế tự động ghi đè Soft Delete
│   └── QuestionCategory.cs       # Entity Class chính
├── Repositories/                 # Tầng giao tiếp Database (EF Core / Dapper)
│   ├── Common/
│   │   ├── BaseRepository.cs     # Repository dùng chung chứa các phương thức CRUD
│   │   └── IUnitOfWork.cs / UnitOfWork.cs
│   ├── Interfaces/
│   │   └── IQuestionCategoryRepository.cs
│   └── Implementations/
│       └── QuestionCategoryRepository.cs
├── Services/                     # Tầng xử lý Logic Nghiệp Vụ (Business Logic Layer)
│   ├── Interfaces/
│   │   └── IQuestionCategoryService.cs
│   └── Implementations/
│       └── QuestionCategoryService.cs
├── Extensions/
│   └── ServicesRegister.cs       # Đăng ký Dependency Injection (DI) cho Repository & Service
└── Program.cs                    # File khởi tạo ứng dụng, cấu hình Auth, Cors, Database...
```

---

## II. Quy Trình 7 Bước Xây Dựng CRUD Tại Backend

### BƯỚC 1: Tạo Entity Model & Cấu hình EF Core Configuration

1. **Tạo Entity Class**:
   Khai báo trong thư mục `Models/`. Đối với các bảng chuẩn có Code và Name, hãy kế thừa từ `StandardEntity<TKey>` (đã kế thừa sẵn Id từ `BaseEntity` và các trường audit từ `AuditableEntity`).
   *Ví dụ: [QuestionCategory.cs](file:///d:/SEP490-G31/DoAn/be/sep490_be/Models/QuestionCategory.cs)*
   ```csharp
   public class QuestionCategory : StandardEntity<int>
   {
       public string? Description { get; set; }
       public virtual ICollection<Question> Questions { get; set; } = new List<Question>();
   }
   ```

2. **Cấu hình Fluent API (Configuration Class)**:
   Tạo file cấu hình trong `Models/Configurations/` để thiết lập ánh xạ cơ sở dữ liệu. Cấu hình này chứa bộ lọc toàn cục tự động loại trừ các bản ghi bị xóa mềm (`IsDeleted == true`).
   *Ví dụ: [QuestionCategoryConfiguration.cs](file:///d:/SEP490-G31/DoAn/be/sep490_be/Models/Configurations/QuestionCategoryConfiguration.cs)*
   ```csharp
   public class QuestionCategoryConfiguration : IEntityTypeConfiguration<QuestionCategory>
   {
       public void Configure(EntityTypeBuilder<QuestionCategory> builder)
       {
           builder.ToTable("question_categories");
           builder.HasKey(x => x.Id);
           builder.Property(x => x.Code).IsRequired().HasMaxLength(50);
           builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
           builder.Property(x => x.Description).HasMaxLength(1000);

           // Tự động bỏ qua các bản ghi đã xóa mềm (soft-deleted) ở mọi truy vấn
           builder.HasQueryFilter(x => !x.IsDeleted);
       }
   }
   ```

3. **Khai báo DbSet trong DbContext**:
   Khai báo thuộc tính `DbSet<QuestionCategory>` trong [ApplicationDbContext.cs](file:///d:/SEP490-G31/DoAn/be/sep490_be/Models/ApplicationDbContext.cs). `ApplicationDbContext` sử dụng cơ chế nạp tự động qua `ApplyConfigurationsFromAssembly` và xử lý Soft Delete tự động qua ghi đè `SaveChanges`/`SaveChangesAsync` để chuyển trạng thái EntityState.Deleted sang EntityState.Modified kèm thuộc tính `IsDeleted = true`.

---

### BƯỚC 2: Tạo DTOs (Data Transfer Objects)

Tạo thư mục DTO riêng cho entity (ví dụ: `DTO/QuestionCategory/`).
* **`QuestionCategoryDto`**: Chứa dữ liệu phản hồi trả về cho client.
* **`QuestionCategorySaveDto`**: Dùng cho hành động tạo mới (Create) và cập nhật (Edit).
  * *Lưu ý*: Có thuộc tính `TextSearch` tự động tính toán từ các trường khác (thông qua `StringHelper.GenerateTextSearch`) để hỗ trợ tìm kiếm nhanh/không dấu.
  * *Ví dụ: [QuestionCategorySaveDto.cs](file:///d:/SEP490-G31/DoAn/be/sep490_be/DTO/QuestionCategory/QuestionCategorySaveDto.cs)*:
    ```csharp
    public class QuestionCategorySaveDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string TextSearch => StringHelper.GenerateTextSearch(Code, Name, Description);
    }
    ```
* **`QuestionCategorySearchDto`**: Kế thừa từ `BaseSearchDto` phục vụ việc phân trang và lọc từ khóa.

---

### BƯỚC 3: Tạo Repository Interface & Implementation

Kế thừa từ cơ chế repository chung của dự án để tái sử dụng toàn bộ hàm CRUD cơ bản (Add, Update, Delete, FindAll, FindByCondition, Deactive).
* **Interface**: *[IQuestionCategoryRepository.cs](file:///d:/SEP490-G31/DoAn/be/sep490_be/Repositories/Interfaces/IQuestionCategoryRepository.cs)*
  ```csharp
  public interface IQuestionCategoryRepository : IBaseRepository<QuestionCategory, ApplicationDbContext>
  {
  }
  ```
* **Implementation**: *[QuestionCategoryRepository.cs](file:///d:/SEP490-G31/DoAn/be/sep490_be/Repositories/Implementations/QuestionCategoryRepository.cs)*
  ```csharp
  public class QuestionCategoryRepository : BaseRepository<QuestionCategory, ApplicationDbContext>, IQuestionCategoryRepository
  {
      public QuestionCategoryRepository(ApplicationDbContext context, IUnitOfWork unitOfWork) : base(context, unitOfWork)
      {
      }
  }
  ```

---

### BƯỚC 4: Tạo Service Interface & Service Implementation

Tầng service chịu trách nhiệm xử lý logic nghiệp vụ, thực hiện xác thực (validation) dữ liệu và chuyển đổi (mapping) giữa Entity và DTO.
* **Interface**: *[IQuestionCategoryService.cs](file:///d:/SEP490-G31/DoAn/be/sep490_be/Services/Interfaces/IQuestionCategoryService.cs)* định nghĩa các phương thức nghiệp vụ: `GetAllAsync`, `GetByIdAsync`, `CreateAsync`, `EditAsync`, `DeleteAsync`, `DeactiveAsync`.
* **Implementation**: *[QuestionCategoryService.cs](file:///d:/SEP490-G31/DoAn/be/sep490_be/Services/Implementations/QuestionCategoryService.cs)*
  * **Xử lý Validation**: Thực hiện các quy tắc kiểm tra rỗng, kiểm tra độ dài chuỗi và kiểm tra trùng lặp mã/tên trong database. Trả về mã lỗi chuẩn (ví dụ: `ERR_CODE_DUPLICATE`).
  * **Sử dụng Mapster**: Gọi `dto.Adapt<QuestionCategory>()` để map nhanh từ DTO sang Entity.
  * **Manual Mapping**: Định nghĩa hàm `MapToDto` thủ công để kiểm soát dữ liệu trả về cho client.
  * **Cơ chế phản hồi**: Trả về `ApiResponse<T>` với trạng thái tương ứng và thông điệp dịch của API (ví dụ: `CREATE_CATEGORY_SUCCESS`).

---

### BƯỚC 5: Đăng ký Service & Repository vào DI Container

Mở file *[ServicesRegister.cs](file:///d:/SEP490-G31/DoAn/be/sep490_be/Extensions/ServicesRegister.cs)* và thêm các dòng đăng ký kiểu `Scoped`:
```csharp
// Repositories
services.AddScoped<IQuestionCategoryRepository, QuestionCategoryRepository>();

// Services
services.AddScoped<IQuestionCategoryService, QuestionCategoryService>();
```

---

### BƯỚC 6: Khai báo Quyền Hạn (Permissions)

Mở file *[Permissions.cs](file:///d:/SEP490-G31/DoAn/be/sep490_be/DTO/Common/Permissions.cs)* và tạo nhóm quyền tương ứng với thực thể mới để thực hiện phân quyền cấp API:
```csharp
public static class QuestionCategory
{
    public const string QuestionCategoryPage = "QuestionCategory";
    public const string QuestionCategory_View = "QuestionCategory.View";
    public const string QuestionCategory_Create = "QuestionCategory.Create";
    public const string QuestionCategory_Edit = "QuestionCategory.Edit";
    public const string QuestionCategory_Delete = "QuestionCategory.Delete";
}
```

---

### BƯỚC 7: Tạo Web API Controller

Tạo Controller trong thư mục `Controllers/`. 
* Kế thừa `ControllerBase` và gắn các thuộc tính `[ApiController]`, `[Route("api/[controller]")]`, `[Authorize]`.
* Sử dụng attribute phân quyền tùy biến `[HasPermission(...)]` để giới hạn quyền truy cập cho từng Endpoint.
* Gọi qua Service Layer và trả về kết quả định dạng `StatusCode(response.StatusCode, response)`.
* *Ví dụ: [QuestionCategoryController.cs](file:///d:/SEP490-G31/DoAn/be/sep490_be/Controllers/QuestionCategoryController.cs)*

---

## III. Cơ Chế Bản Địa Hóa (Localization) Tại Backend

Backend **không dịch trực tiếp** văn bản thông báo sang Tiếng Anh hay Tiếng Việt để giữ cho máy chủ hoạt động nhẹ nhàng và độc lập ngôn ngữ.
* **Quy chuẩn**: Backend luôn trả về một **mã định danh thông điệp duy nhất** dạng chữ in hoa viết liền (ví dụ: `ERR_CODE_DUPLICATE`, `CREATE_CATEGORY_SUCCESS`) qua thuộc tính `message` của đối tượng phản hồi `ApiResponse`.
* Mọi việc dịch hiển thị sẽ được giao hoàn toàn cho Frontend xử lý dựa trên ngôn ngữ người dùng lựa chọn.

---

## IV. Các Design Pattern Áp Dụng Tại Backend

* **Repository Pattern & Unit of Work**: Tách rời việc giao tiếp DB và nghiệp vụ, quản lý giao dịch (Transaction) qua `IUnitOfWork`.
* **Dependency Injection (DI)**: Đăng ký dịch vụ lỏng lẻo qua `ServicesRegister.cs`.
* **Soft Delete (Global Query Filter)**: EF Core tự động loại trừ các bản ghi có `IsDeleted == true`.
* **Interceptor (Audit Logs)**: Tự ghi nhận `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy` trong DbContext mà không cần gán thủ công tại tầng Service.

