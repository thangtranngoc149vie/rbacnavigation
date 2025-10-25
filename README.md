# RBAC Navigation API

RBAC Navigation API cung cấp các API điều hướng theo tài liệu "Api Rbac Navigation V1.1" với xác thực JWT, kiểm soát tenant và logging bằng Serilog.

## Yêu cầu hệ thống
- .NET SDK 8.0
- PostgreSQL (được truy cập thông qua Npgsql)

## Thiết lập dự án
1. Cập nhật chuỗi kết nối mặc định trong `RbacNavigation.Api/appsettings.json` hoặc thiết lập biến môi trường `ConnectionStrings__Default`.
2. Khởi chạy database cần thiết cho repository (hiện tại các repository đọc dữ liệu điều hướng từ PostgreSQL).
3. Khởi chạy ứng dụng:
   ```bash
   dotnet run --project RbacNavigation.Api
   ```
4. Truy cập Swagger UI tại `https://localhost:5001/swagger` để kiểm thử các API.

## Logging
Ứng dụng sử dụng Serilog với cấu hình trong `appsettings.json` để ghi log ra console và bật tính năng `UseSerilogRequestLogging` cho toàn bộ request. Các controller chính (`NavigationController`, `NavigationConfigsController`) đã được chèn log chi tiết để theo dõi luồng xử lý.

## Các API hiện có (phiên bản 1.1)
| Phương thức | Đường dẫn | Mô tả |
|-------------|-----------|-------|
| `GET` | `/api/v1/navigation` | Trả về menu điều hướng cho người dùng hiện tại dựa trên quyền và tenant. |
| `GET` | `/api/v1/navigation/preview` | Xem trước menu của một vai trò khác trong cùng tổ chức (cần quyền admin). |
| `GET` | `/api/v1/configs/navigation` | Lấy cấu hình điều hướng hiện tại cho tenant (cần quyền admin). |
| `PUT` | `/api/v1/configs/navigation` | Cập nhật cấu hình điều hướng sau khi hợp lệ JSON và quyền admin. |

## Kiểm thử
Sử dụng `dotnet build` để đảm bảo dự án biên dịch thành công. Có thể bổ sung kiểm thử tự động trong tương lai.

## Lịch sử thay đổi gần đây
- Thêm Serilog vào pipeline để theo dõi hoạt động các API.
- Hoàn thiện các API điều hướng và cấu hình điều hướng theo tài liệu phiên bản 1.1.
