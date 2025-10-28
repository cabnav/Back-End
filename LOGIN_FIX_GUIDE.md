# 🔧 HƯỚNG DẪN FIX LỖI ĐĂNG NHẬP

## 🎯 Vấn đề:
Sau khi sửa logic reservation, bạn không thể đăng nhập với thông báo "Invalid email or password".

## 🔍 Nguyên nhân:
- DataSeeder tạo user với password plain text (`"123456"`)
- AuthService sử dụng SHA256 hash để verify password
- Khi đăng nhập, password `"12345"` được hash nhưng không khớp với password đã hash trong DB

## ✅ Giải pháp:

### Cách 1: Sử dụng API Fix Database (Khuyến nghị)

1. **Gọi API fix database:**
   ```http
   POST https://localhost:7035/api/DatabaseFix/fix-passwords
   ```

2. **Test đăng nhập:**
   ```http
   POST https://localhost:7035/api/Auth/login
   Content-Type: application/json
   
   {
     "email": "chinh22@gmail.com",
     "password": "12345"
   }
   ```

### Cách 2: Sử dụng file test

1. Mở file `test_login_fix.http`
2. Chạy từng request theo thứ tự:
   - Fix database
   - Test đăng nhập

### Cách 3: Fix thủ công trong Database

Nếu bạn có quyền truy cập SQL Server:

```sql
-- Cập nhật password cho user chinh22@gmail.com
UPDATE Users 
SET Password = 'jZae727K08KaOmKSgOaGzww/XVqGr/PKEgIMQrcPki8g='  -- SHA256 hash của "12345"
WHERE Email = 'chinh22@gmail.com'

-- Hoặc tạo user mới nếu chưa có
INSERT INTO Users (Name, Email, Password, Phone, Role, WalletBalance, BillingType, MembershipTier, CreatedAt)
VALUES ('Chinh User', 'chinh22@gmail.com', 'jZae727K08KaOmKSgOaGzww/XVqGr/PKEgIMQrcPki8g=', '0901234567', 'driver', 500000, 'prepaid', 'standard', GETDATE())
```

## 🎉 Sau khi fix:

Bạn có thể đăng nhập với:
- **Email:** `chinh22@gmail.com`
- **Password:** `12345`

Hoặc các user mẫu khác:
- **Email:** `a@example.com`, **Password:** `123456`
- **Email:** `b@example.com`, **Password:** `123456`

## 📝 Lưu ý:

1. **Password hash:** Tất cả password đều được hash bằng SHA256
2. **Database:** Đảm bảo SQL Server đang chạy
3. **Connection:** Kiểm tra connection string trong `appsettings.json`
4. **Migration:** Nếu cần thêm ConnectorType vào DriverProfile, chạy migration sau khi stop ứng dụng

## 🔄 Workflow đặt chỗ mới:

Sau khi đăng nhập thành công, bạn có thể test logic đặt chỗ mới:

1. **Tìm trạm sạc phù hợp:**
   ```http
   POST /api/reservations/search-stations
   {
     "connectorType": "CCS",
     "date": "2024-01-15T00:00:00Z",
     "latitude": 10.762622,
     "longitude": 106.660172,
     "radiusKm": 10
   }
   ```

2. **Lấy điểm sạc phù hợp:**
   ```http
   GET /api/reservations/stations/1/compatible-points?connectorType=CCS
   ```

3. **Lấy khung giờ có sẵn:**
   ```http
   GET /api/reservations/points/1/time-slots?date=2024-01-15T00:00:00Z
   ```

4. **Tạo đặt chỗ:**
   ```http
   POST /api/reservations
   {
     "pointId": 1,
     "date": "2024-01-15T00:00:00Z",
     "hour": 14
   }
   ```
