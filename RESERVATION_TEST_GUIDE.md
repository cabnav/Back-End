# 🔧 HƯỚNG DẪN TEST API ĐẶT CHỖ MỚI

## 🚨 Lỗi "Cannot book in the past"

### Nguyên nhân:
- Bạn đang sử dụng ngày/giờ trong quá khứ
- API không cho phép đặt chỗ trong quá khứ

### ✅ Cách fix:

#### 1. **Sử dụng ngày tương lai:**
```json
{
  "pointId": 1,
  "date": "2024-12-25T00:00:00Z",  // ✅ Ngày tương lai
  "hour": 14
}
```

#### 2. **Sử dụng ngày hôm nay với giờ tương lai:**
```json
{
  "pointId": 1,
  "date": "2024-01-15T00:00:00Z",   // ✅ Ngày hôm nay
  "hour": 16                        // ✅ Giờ tương lai (hiện tại là 14h)
}
```

## 🎯 Workflow test đúng:

### Bước 1: Đăng nhập
```http
POST https://localhost:7035/api/Auth/login
{
  "email": "chinh22@gmail.com",
  "password": "12345"
}
```

### Bước 2: Tìm trạm sạc
```http
POST https://localhost:7035/api/reservations/search-stations
{
  "connectorType": "CCS",
  "date": "2024-12-25T00:00:00Z",
  "latitude": 10.762622,
  "longitude": 106.660172,
  "radiusKm": 10
}
```

### Bước 3: Lấy điểm sạc phù hợp
```http
GET https://localhost:7035/api/reservations/stations/1/compatible-points?connectorType=CCS
```

### Bước 4: Lấy khung giờ có sẵn
```http
GET https://localhost:7035/api/reservations/points/1/time-slots?date=2024-12-25T00:00:00Z
```

### Bước 5: Tạo đặt chỗ
```http
POST https://localhost:7035/api/reservations
{
  "pointId": 1,
  "date": "2024-12-25T00:00:00Z",
  "hour": 14
}
```

## 📅 Lưu ý về thời gian:

1. **Ngày:** Phải là ngày hôm nay hoặc tương lai
2. **Giờ:** Nếu là ngày hôm nay, phải là giờ tương lai
3. **Format:** Sử dụng UTC timezone (`Z` ở cuối)
4. **Hour:** Từ 0-23 (24 khung giờ)

## 🔍 Debug thời gian:

### Kiểm tra giờ hiện tại:
```csharp
var now = DateTime.UtcNow;
Console.WriteLine($"Current UTC time: {now:yyyy-MM-dd HH:mm:ss}");
Console.WriteLine($"Current hour: {now.Hour}");
```

### Ví dụ thời gian hợp lệ:
- **Hôm nay 15h:** `"date": "2024-01-15T00:00:00Z", "hour": 15`
- **Ngày mai:** `"date": "2024-01-16T00:00:00Z", "hour": 10`
- **Tuần sau:** `"date": "2024-01-22T00:00:00Z", "hour": 9`

## 🎉 Kết quả mong đợi:

```json
{
  "reservationId": 1,
  "pointId": 1,
  "startTime": "2024-12-25T14:00:00Z",
  "endTime": "2024-12-25T15:00:00Z",
  "status": "booked",
  "createdAt": "2024-01-15T10:30:00Z"
}
```
