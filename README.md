# Hệ thống tra cứu dữ liệu học tập bằng GraphQL (Đề tài 19)

## Kiến trúc dự kiến

```
Client -> GraphQL API
            |  gRPC        gRPC
     Student Service   Course Service
            |               |
        Student DB      Course DB
```

## Trạng thái hiện tại
- [x] Student.Service (cổng 5101): dữ liệu sinh viên, SQLite riêng
- [x] Course.Service (cổng 5102): học phần và đăng ký học, SQLite riêng
- [x] Dockerfile cho từng service và docker-compose.yml
- [ ] GraphQL API
- [ ] Giao tiếp gRPC giữa GraphQL API và hai service

## Công nghệ
C#, .NET 10, ASP.NET Core Minimal API, EF Core SQLite, Docker Compose.

## Chạy hệ thống

```
docker compose up --build -d
docker compose ps
docker compose logs -f
docker compose down
```

Hoặc chạy trực tiếp từng service:

```
dotnet run --project src/Student.Service --urls http://localhost:5101
dotnet run --project src/Course.Service --urls http://localhost:5102
```

Chuỗi kết nối được đọc từ cấu hình (`appsettings.json`) và ghi đè bằng biến môi trường
`ConnectionStrings__StudentDb`, `ConnectionStrings__CourseDb` trong `docker-compose.yml`.

## Endpoint hiện có

| Service | Endpoint | Mô tả |
|---|---|---|
| Student (5101) | GET /health | Kiểm tra trạng thái |
| Student (5101) | GET /api/students?status= | Danh sách, lọc theo trạng thái |
| Student (5101) | GET /api/students/{id} | Chi tiết sinh viên |
| Student (5101) | GET /api/students/by-ids?ids=1,2 | Lấy nhiều sinh viên |
| Student (5101) | PATCH /api/students/{id}/status | Cập nhật trạng thái |
| Course (5102) | GET /health | Kiểm tra trạng thái |
| Course (5102) | GET /api/courses?semester= | Danh sách học phần |
| Course (5102) | GET /api/courses/{id} | Chi tiết học phần |
| Course (5102) | GET /api/enrollments?studentId=&courseId=&semester=&status= | Danh sách đăng ký học |
| Course (5102) | POST /api/enrollments | Đăng ký học phần |

Mã lỗi dùng chung dạng `{ "code": "...", "message": "..." }` với các mã HTTP 400, 404, 409.

## Ghi chú
- Cảnh báo NuGet NU1903 (GHSA-2m69-gcr7-jv3q) từ gói SQLitePCLRaw.lib.e_sqlite3 2.1.11, phụ thuộc gián tiếp của EF Core SQLite. Rủi ro thấp trong môi trường học tập, theo dõi bản vá từ upstream.
- `Enrollment` chỉ lưu `StudentId` làm tham chiếu logic, không có khóa ngoại sang DB của Student.Service.