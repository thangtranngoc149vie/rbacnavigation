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

## Bảo vệ dữ liệu đầu vào
- Repository `NavigationRepository` thực thi truy vấn PostgreSQL thông qua Dapper với `CommandDefinition` và tham số được khai báo kiểu rõ ràng, loại bỏ nội suy chuỗi và bảo vệ khỏi SQL Injection.
- Payload cấu hình điều hướng được làm sạch bởi `NavigationContentSanitizer`: các nhãn/tooltip được HTML-encode, đường dẫn bị loại bỏ nếu sử dụng scheme nguy hiểm (ví dụ `javascript:`) và dữ liệu luôn được lưu xuống cơ sở dữ liệu ở trạng thái an toàn để tránh XSS.
- Khi trả về cấu hình hoặc menu điều hướng, dữ liệu đều được đi qua sanitizer nhằm bảo vệ người dùng ngay cả với cấu hình lịch sử.

## Phân quyền RBAC & ABAC
- Thông tin người dùng hiện tại được nạp một lần mỗi request thông qua `ICurrentUserContextAccessor`, đồng thời sử dụng token hủy liên kết để tôn trọng trạng thái hủy của HTTP.
- `PermissionsAuthorizationHandler` kiểm tra quyền RBAC dựa trên `PermissionSet` giải mã từ JSON vai trò và hỗ trợ cả yêu cầu "tất cả" lẫn "bất kỳ" quyền.
- `OrganizationScopeAuthorizationHandler` đảm bảo mọi thao tác chỉ diễn ra trong phạm vi `org_id` tương ứng (ABAC) và cũng tôn trọng token hủy của request.
- Các controller gọi `IAuthorizationService` để kết hợp linh hoạt RBAC và ABAC theo yêu cầu từng endpoint.

## Các API hiện có (phiên bản 1.1)
| Phương thức | Đường dẫn | Mô tả |
|-------------|-----------|-------|
| `GET` | `/api/v1/navigation` | Trả về menu điều hướng cho người dùng hiện tại dựa trên quyền và tenant. |
| `POST` | `/api/v1/navigation/preview` | Cho phép quản trị viên giả lập quyền, người dùng hoặc bản nháp cấu hình để xem trước menu cùng chẩn đoán. |
| `GET` | `/api/v1/configs/navigation` | Lấy cấu hình điều hướng hiện tại cho tenant (cần quyền admin). |
| `PUT` | `/api/v1/configs/navigation` | Cập nhật cấu hình điều hướng sau khi hợp lệ JSON và quyền admin. |

### Chuẩn hóa `requires`
- Hỗ trợ đồng thời hai “dialect” cho điều kiện hiển thị menu:
  - Dạng tuple gốc: `"requires": [["crm","tickets","read"],["project","timeline","read"]]`.
  - Dạng chuỗi phẳng: `"requires": ["crm:tickets:read","project:timeline:read"]`.
- Mỗi phần tử trong `requires` được xem là một lựa chọn (quan hệ `OR`). Khi evaluate:
  - Tất cả tuple được chuẩn hóa thành chuỗi `domain:area:action`.
  - Hệ thống kiểm tra quyền chính xác hoặc wildcard hậu tố (`crm:tickets:*`).
  - Nếu danh sách rỗng hoặc bị bỏ qua thì item được xem là public.

### `POST /api/v1/navigation/preview`
- Cho phép truyền bản nháp cấu hình (`draft_nav_value`), giả lập user/role (`as_user_id`, `as_role_id`) hoặc bộ quyền (`as_permissions`).
- Các tùy chọn `include_hidden` (mặc định `true`) và `return_reason` (mặc định `true`) kiểm soát việc trả về item ẩn và thông điệp chẩn đoán.
- Mỗi item trong response bao gồm:
  - `visible`: kết quả evaluate quyền.
  - `requires`: danh sách scope dạng chuỗi.
  - `reason`: thông điệp `matched:` hoặc `missing:` cho scope đầu tiên, hoặc `public` nếu không cần quyền.
  - `matched_scope`: scope thực tế thỏa mãn (khi có).

## Kiểm thử
Sử dụng `dotnet build` để đảm bảo dự án biên dịch thành công. Có thể bổ sung kiểm thử tự động trong tương lai.

## Ghi chú phiên bản
- **v1.1.2**
  - Chuẩn hóa xử lý `requires` với cả tuple và chuỗi phẳng, hỗ trợ wildcard `*` ở hành động.
  - Tách service `NavigationService` để gom logic lấy dữ liệu, sanitize và evaluate quyền cho cả API navigation và preview.
  - Nâng cấp API preview sang `POST`, bổ sung khả năng giả lập quyền/bản nháp cùng lý do hiển thị từng menu.
- **v1.1.1**
  - Tài liệu hóa các tính năng RBAC/ABAC, logging Serilog và các biện pháp chống SQL Injection/XSS.
  - Bổ sung ghi chú phiên bản trong README để dễ dàng theo dõi thay đổi.
- **v1.1.0**
  - Phát hành lần đầu các API điều hướng theo tài liệu "Api Rbac Navigation V1.1".

## Lịch sử thay đổi gần đây
- Tăng cường bảo mật đầu vào với tham số hóa truy vấn SQL và sanitizer chống XSS cho cấu hình điều hướng.
- Bổ sung RBAC/ABAC handler cho phép kết hợp kiểm tra quyền và phạm vi tổ chức trong từng request.
- Thêm Serilog vào pipeline để theo dõi hoạt động các API.
- Hoàn thiện các API điều hướng và cấu hình điều hướng theo tài liệu phiên bản 1.1.
