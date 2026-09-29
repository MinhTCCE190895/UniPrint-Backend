# UniPrint - N-Tier Architecture Project Skeleton

Hệ thống in ấn trực tuyến và chia sẻ tài liệu học tập sinh viên (UniPrint Platform) được xây dựng trên nền tảng **.NET 8 (LTS)** theo kiến trúc **N-Tier (N-Layer: Presentation $\rightarrow$ Business $\rightarrow$ Data Access)** dựa trên tài liệu **UniPrint - Tech Scope.docx**.

---

## 1. Cấu Trúc Dự Án (N-Tier Architecture)

```text
UniPrint/
├── UniPrint.DataAccess/                     # [TẦNG DATA ACCESS (DAL)]
│   ├── Entities/                            # Mỗi Entity là 1 file .cs riêng biệt:
│   │   ├── BaseEntity.cs
│   │   ├── User.cs, Document.cs, PrintOrder.cs, PrintOption.cs
│   │   ├── Shelf.cs, PickupQR.cs, Payment.cs, PriceConfig.cs
│   │   └── StudyMaterial.cs, Subject.cs, Category.cs, MaterialReview.cs, MaterialReport.cs, Favorite.cs
│   ├── Enums/                               # Mỗi Enum là 1 file .cs riêng biệt:
│   │   ├── UserRole.cs, PrintOrderStatus.cs, PaymentMethod.cs, PaymentStatus.cs, MaterialStatus.cs
│   │   └── (Đã tách rời hoàn toàn, không gộp chung)
│   ├── Context/                             # UniPrintDbContext.cs (EF Core cấu hình quan hệ & Seed Data)
│   └── Repositories/                        # GenericRepository<T>, UnitOfWork.cs
│
├── UniPrint.Business/                       # [TẦNG BUSINESS LOGIC (BLL)]
│   ├── Common/                              # ApiResponse<T>
│   ├── DTOs/                                # Auth, PrintOrder, Document, StudyHub DTOs
│   └── Services/                            # Logic nghiệp vụ bám sát Tech Scope:
│       ├── AuthService.cs                   # Đăng ký, đăng nhập JWT, phân quyền (F01, NFR02)
│       ├── DocumentService.cs               # Upload, quản lý metadata tài liệu (F02)
│       ├── PriceCalculatorService.cs        # Tính giá in tức thời và khóa giá theo bảng giá (BR02, NFR01)
│       ├── PrintOrderService.cs             # Tạo đơn in, xử lý quy trình in, gán kệ, quét mã QR (BR02, EC04, EC06)
│       └── StudyHubService.cs               # Chia sẻ tài liệu, kiểm duyệt Admin (BR04), đánh giá sao (BR05)
│
├── UniPrint.API/                            # [TẦNG PRESENTATION (API)]
│   ├── Controllers/                         # RESTful Web API Controllers
│   │   ├── AuthController.cs                # /api/auth/register, /api/auth/login, /api/auth/me
│   │   ├── DocumentsController.cs           # /api/documents/upload, /api/documents/my-documents
│   │   ├── PrintOrdersController.cs         # /api/printorders (tạo đơn, hàng đợi Staff, gán kệ, quét QR)
│   │   └── StudyHubController.cs            # /api/studyhub (tìm kiếm tài liệu duyệt, upload, duyệt bài, review)
│   ├── Middlewares/                         # ExceptionHandlingMiddleware (bắt lỗi tập trung)
│   ├── Program.cs                           # Cấu hình DI, JWT Bearer Authentication, Swagger UI & CORS
│   └── appsettings.json                     # Chuỗi kết nối SQL Server và JWT Secret
│
├── UniPrint.sln                             # File Solution Visual Studio truyền thống
└── UniPrint.slnx                            # File Solution Visual Studio 2022+ mới nhất
```

---

## 2. Tài Khoản Mặc Định Đã Seed Sẵn Trong Database (Mật khẩu: `123456`)

| Role | Email | Mật khẩu | Quyền hạn chính |
| :--- | :--- | :--- | :--- |
| **Admin** | `admin@uniprint.edu.vn` | `123456` | Quản lý hệ thống, duyệt tài liệu Study Hub, quản trị giá in |
| **Staff** | `staff@uniprint.edu.vn` | `123456` | Tiếp nhận đơn in, in tài liệu, gán kệ (A1, A2, B1), quét QR giao hàng |
| **Student** | `student@uniprint.edu.vn` | `123456` | Upload file, cấu hình in, tạo đơn in, quét QR nhận tài liệu, dùng Study Hub |

---

## 3. Hướng Dẫn Chạy Dự Án

### Bước 1: Cấu hình chuỗi kết nối Database
Mở file `UniPrint/UniPrint.API/appsettings.json` và kiểm tra chuỗi kết nối SQL Server của máy bạn:
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Database=UniPrintDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
}
```

### Bước 2: Tạo Migration & Cập Nhật Database
Chạy lệnh sau tại thư mục gốc `UniPrint`:
```bash
dotnet ef migrations add InitialCreate --project UniPrint.DataAccess --startup-project UniPrint.API
dotnet ef database update --project UniPrint.DataAccess --startup-project UniPrint.API
```

### Bước 3: Khởi chạy API
```bash
dotnet run --project UniPrint.API
```

Sau khi chạy, mở trình duyệt truy cập Swagger UI:
- **Swagger URL:** `https://localhost:7000/swagger` (hoặc port hiển thị trên terminal).
- Đăng nhập bằng `POST /api/auth/login` $\rightarrow$ copy `accessToken` $\rightarrow$ bấm nút **Authorize** trên Swagger và paste vào với tiền tố `Bearer {token}` để kiểm thử toàn bộ API.
