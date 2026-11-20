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
- [x] Student.Service (cổng 5101): dữ liệu sinh viên, DB SQLite riêng
- [x] Course.Service (cổng 5102): học phần và đăng ký học, DB SQLite riêng
- [ ] GraphQL API
- [ ] Giao tiếp gRPC

## Chạy hệ thống
```
docker compose up --build -d
docker compose logs -f
docker compose down
```