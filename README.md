# 🖨️ UniPrint - Nền Tảng In Ấn Trực Tuyến & Kho Học Liệu Sinh Viên

[![.NET 8](https://img.shields.io/badge/.NET-8.0%20LTS-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)
[![EF Core](https://img.shields.io/badge/EF%20Core-8.0-blue?style=flat-square)](https://learn.microsoft.com/ef/core/)
[![Architecture](https://img.shields.io/badge/Architecture-N--Tier%20Layered-success?style=flat-square)](#1-kiến-trúc-hệ-thống)
[![Frontend](https://img.shields.io/badge/UI-Razor%20Pages%20Bootstrap-orange?style=flat-square)](#3-giao-diện-frontend-razor-pages)
[![Database](https://img.shields.io/badge/Database-MS%20SQL%20Server-red?style=flat-square)](https://www.microsoft.com/sql-server)

**UniPrint** là giải pháp phần mềm toàn diện giải quyết bài toán in ấn tài liệu trong trường đại học, kết hợp nền tảng chia sẻ học liệu số giữa sinh viên với nhau.

> 📖 **Xem đặc tả chi tiết nghiệp vụ, sơ đồ Use Case, Sequence Diagram & ERD tại:**  
> 👉 [**docs/FEATURES_AND_USE_CASES.md**](docs/FEATURES_AND_USE_CASES.md)

---

## 1. Kiến Trúc Hệ Thống (N-Tier Architecture)

Dự án được tổ chức theo mô hình phân tầng **N-Tier chuẩn mực**, chạy trên **.NET 8 LTS**:

```text
UniPrint/
├── UniPrint.DataAccess/         # [TẦNG DATA ACCESS (DAL)]
│   ├── Entities/                # 15 Entities độc lập (User, Document, PrintOrder, Shelf, PickupQR...)
│   ├── Enums/                   # 5 Enums hệ thống (UserRole, PrintOrderStatus, PaymentMethod...)
│   ├── Context/                 # UniPrintDbContext (EF Core, cấu hình quan hệ & nạp sẵn Seed Data)
│   └── Repositories/            # GenericRepository<T>, UnitOfWork quản lý Transaction
│
├── UniPrint.Business/           # [TẦNG BUSINESS LOGIC (BLL)]
│   ├── Common/                  # ApiResponse<T> chuẩn hóa dữ liệu trả về
│   ├── DTOs/                    # Data Transfer Objects cho từng Feature
│   └── Services/                # Xử lý nghiệp vụ chính:
│       ├── AuthService.cs       # Đăng ký, đăng nhập JWT Bearer, băm mật khẩu BCrypt (F01)
│       ├── DocumentService.cs   # Upload và đọc metadata tài liệu (F02)
│       ├── PriceCalculator.cs   # Thuật toán tính giá in tức thời và khóa giá chốt đơn (BR02)
│       ├── PrintOrderService.cs # Tạo đơn in, xử lý hàng đợi Staff, gán kệ, quét mã QR (BR02, EC06)
│       └── StudyHubService.cs   # Chia sẻ tài liệu, Admin duyệt bài (BR04), đánh giá sao (BR05)
│
├── UniPrint.API/                # [TẦNG WEB API (Backend RESTful)]
│   ├── Controllers/             # RESTful API Controllers cho hệ thống bên ngoài / Mobile
│   ├── Middlewares/             # ExceptionHandlingMiddleware bắt lỗi tập trung
│   └── Program.cs               # Cấu hình JWT Bearer, Swagger UI & CORS
│
└── UniPrint.Web/                # [TẦNG PRESENTATION (Frontend Razor Pages)]
    ├── Pages/
    │   ├── Shared/_Layout.cshtml# Navbar điều hướng tích hợp cả 5 phân hệ
    │   ├── Auth/                # Đăng nhập, đăng ký, thông tin tài khoản cá nhân
    │   ├── Student/             # Upload file, chọn cấu hình in, xem giá realtime, quản lý đơn
    │   ├── Staff/               # Dashboard hàng đợi in ấn, cập nhật tiến độ, gán kệ lưu trữ
    │   ├── Pickup/              # Quét mã QR nhận tài liệu, thanh toán tiền mặt / VietQR
    │   └── StudyHub/            # ⭐ Kho tài liệu học tập, xem trước, tải về, đánh giá, duyệt bài
    └── Program.cs               # Cấu hình Cookie Authentication & Dependency Injection
```

---

## 2. Các Tính Năng & Phân Hệ Cốt Lõi

### 🔹 Phân hệ 1: Sinh Viên (Student Flow - Luồng in ấn chính)
* **Upload tài liệu cá nhân:** Tải file PDF, DOCX; hệ thống tự động đọc số trang và kích thước file.
* **Cấu hình in ấn linh hoạt:** Chọn in màu/đen trắng, in 1 mặt/2 mặt (giảm 10%), đóng gáy sách hoặc bấm kim góc.
* **Tính giá tức thời & Khóa giá (BR02):** Giá được tính trực tiếp và chốt cố định khi tạo đơn, không bị ảnh hưởng nếu bảng giá hệ thống thay đổi sau đó.
* **Mã QR nhận tài liệu (Pickup QR):** Đơn in hoàn tất sẽ sinh mã QR có thời hạn 7 ngày để sinh viên đưa cho nhân viên quét tại quầy.

### 🔹 Phân hệ 2: Nhân Viên (Staff Operations)
* **Hàng đợi in ấn (Staff Queue):** Xem danh sách các đơn đang chờ xử lý, sắp xếp ưu tiên theo thời gian.
* **Tiến trình in ấn:** Bấm nhận đơn $\rightarrow$ chuyển trạng thái sang `Printing` (lúc này sinh viên không thể hủy đơn).
* **Quản lý Kệ lưu trữ (Shelf):** Sau khi in xong, nhân viên chọn kệ còn chỗ (`A1`, `A2`, `B1`) để đặt tài liệu và chuyển đơn sang `ReadyForPickup`. Hệ thống tự động kiểm tra sức chứa tối đa (`EC06`).
* **Quét mã QR giao tài liệu:** Nhân viên quét mã QR của sinh viên, hệ thống kiểm tra hạn (`EC04`), đánh dấu hoàn tất đơn (`Completed`) và tự động giải phóng vị trí trên kệ.

### 🔹 Phân hệ 3: Study Hub (Kho học liệu số - Module độc lập)
* **Chia sẻ tài liệu:** Sinh viên đăng tài liệu vào kho dùng chung (mặc định ở trạng thái `Pending` chờ duyệt).
* **Kiểm duyệt (Admin):** Admin xem danh sách bài chờ và bấm Duyệt (`Approved`) hoặc Từ chối (`Rejected`).
* **Tìm kiếm & Bộ lọc:** Tìm kiếm theo từ khóa, lọc theo Môn học (`PRN231`, `SWD392`...) và Thể loại (Slide, Đề thi, Tóm tắt).
* **Đánh giá & Tương tác:** Sinh viên đánh giá 1–5 ⭐ (mỗi SV chỉ đánh giá 1 lần/bài), báo cáo vi phạm, thêm vào mục Yêu thích.

> 💡 **Tính độc lập:** Module Study Hub được thiết kế hoàn toàn tách biệt. Khi đem dự án sang môn học khác (như làm App Mobile chỉ tập trung vào in ấn), nhóm có thể **cắt bỏ toàn bộ module Study Hub** mà luồng in ấn chính vẫn chạy mượt mà 100%!

---

## 3. Tài Khoản Thử Nghiệm Mặc Định (Seed Data)

Khi khởi tạo database, hệ thống đã nạp sẵn 3 tài khoản mẫu với mật khẩu mặc định là **`123456`**:

| Vai trò | Email đăng nhập | Mật khẩu | Quyền hạn chính |
| :--- | :--- | :---: | :--- |
| **Admin** | `admin@uniprint.edu.vn` | `123456` | Quản trị người dùng, duyệt tài liệu Study Hub, cấu hình bảng giá |
| **Staff** | `staff@uniprint.edu.vn` | `123456` | Xử lý hàng đợi in, in ấn, xếp kệ (`A1`, `A2`, `B1`), quét QR giao tài liệu |
| **Student** | `student@uniprint.edu.vn` | `123456` | Upload file, cấu hình in, tạo đơn, quét QR lấy đồ, dùng Study Hub |

---

## 4. Hướng Dẫn Cài Đặt & Chạy Dự Án

### Yêu cầu môi trường
* **.NET 8 SDK** trở lên (Khuyến nghị bản LTS).
* **Microsoft SQL Server** (2016 trở lên hoặc SQL Server Express / LocalDB).
* **Visual Studio 2022** (v17.8 trở lên) hoặc VS Code / JetBrains Rider.

### Bước 1: Clone Repository
```bash
git clone https://github.com/MinhTCCE190895/UniPrint-Backend.git
cd UniPrint-Backend
```

### Bước 2: Cấu hình chuỗi kết nối SQL Server
Mở file `UniPrint.Web/appsettings.json` và `UniPrint.API/appsettings.json`, kiểm tra `DefaultConnection` phù hợp với máy tính của bạn:
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Database=UniPrintDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
}
```

### Bước 3: Tạo Database & Nạp Seed Data (EF Core Migration)
Chạy lệnh sau tại thư mục gốc của Solution:
```bash
dotnet ef migrations add InitialCreate --project UniPrint.DataAccess --startup-project UniPrint.API
dotnet ef database update --project UniPrint.DataAccess --startup-project UniPrint.API
```

### Bước 4: Khởi chạy dự án

#### Cách 1: Chạy trực tiếp bằng Visual Studio (F5)
1. Mở file [UniPrint.sln](UniPrint.sln).
2. Chuột phải vào project **`UniPrint.Web`** $\rightarrow$ chọn **Set as Startup Project**.
3. Nhấn **F5** (hoặc `Ctrl + F5`) để mở giao diện web trên trình duyệt (`https://localhost:7xxx`).

#### Cách 2: Chạy bằng dòng lệnh (CLI)
```bash
# Chạy giao diện Web (Razor Pages):
dotnet run --project UniPrint.Web

# Hoặc chạy riêng Web API backend (kèm Swagger):
dotnet run --project UniPrint.API
```

* **Giao diện Web:** `https://localhost:7100` (hoặc cổng xuất hiện trên terminal)
* **Swagger API UI:** `https://localhost:7000/swagger`

---

## 5. Phân Công Công Việc Nhóm (Task Matrix)
Dự án đã được phân chia đều thành 5 Feature độc lập, mỗi thành viên đảm nhận Full-stack (cả Backend lẫn Frontend) với trọng số chuẩn hóa **20 điểm / người**:
* Chi tiết bảng phân chia, độ khó, điểm số và deadline xem tại file: [UniPrint_Task_Assignment.xlsx](../UniPrint_Task_Assignment.xlsx).
