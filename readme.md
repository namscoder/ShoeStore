# ShoeStore

Web quản lý bán giày - ASP.NET Core MVC (.NET 10) + SQL Server.

## Chạy trên máy mới

1. Clone repo, mở bằng Visual Studio.
2. Đặt mật khẩu admin (bắt buộc):
```
   dotnet user-secrets set "SeedAdmin:Password" "<mật khẩu>"
```
3. Nếu SQL Server của máy KHÔNG phải `localhost`, đặt lại chuỗi kết nối:
```
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=<tên server>;Database=ShoeStoreDb;Trusted_Connection=True;TrustServerCertificate=True;"
```
   - SQL Express: `localhost\SQLEXPRESS`
   - LocalDB: `(localdb)\MSSQLLocalDB`
4. Chạy web. Database và tài khoản admin được tạo tự động.

Đăng nhập admin: username `admin`, mật khẩu là mật khẩu đã đặt ở bước 2.