# Hệ thống đặt chỗ mới - EV Charging Station

## Tổng quan

Hệ thống đặt chỗ đã được cập nhật để hỗ trợ workflow mới:
1. **Tìm trạm sạc phù hợp** - Người dùng tự chọn loại cổng sạc của xe
2. **Chọn điểm sạc** bất kỳ tại trạm (miễn là có cổng phù hợp)
3. **Chọn khung giờ** từ 24 khung giờ trong ngày (mỗi khung 1 tiếng)

## Các thay đổi chính

### 1. DriverProfile Entity
- **Không cần** thêm ConnectorType vào DriverProfile
- Người dùng tự chọn loại cổng sạc khi tìm kiếm trạm

### 2. DTOs mới
- `StationSearchRequest`: Request tìm kiếm trạm sạc
- `StationSearchResponse`: Response danh sách trạm phù hợp
- `CompatibleChargingPointDTO`: Thông tin điểm sạc phù hợp
- `TimeSlotDTO`: Thông tin khung giờ có sẵn

### 3. ReservationRequest cập nhật
- Thêm `Date`: Ngày đặt chỗ
- Thêm `Hour`: Giờ bắt đầu (0-23)
- `StartTime` và `EndTime` được tính tự động
- Giữ tương thích ngược với `LegacyStartTime` và `DurationMinutes`

### 4. Service mới
- `IStationSearchService`: Tìm kiếm trạm sạc phù hợp
- `StationSearchService`: Implementation của service trên

### 5. API Endpoints mới

#### Tìm kiếm trạm sạc phù hợp
```
POST /api/reservations/search-stations
```
**Request:**
```json
{
  "connectorType": "CCS",
  "date": "2024-01-15T00:00:00Z",
  "latitude": 10.762622,
  "longitude": 106.660172,
  "radiusKm": 10
}
```

#### Lấy điểm sạc phù hợp tại trạm
```
GET /api/reservations/stations/{stationId}/compatible-points?connectorType={type}
```

#### Lấy khung giờ có sẵn
```
GET /api/reservations/points/{pointId}/time-slots?date={date}
```

#### Tạo đặt chỗ (logic mới)
```
POST /api/reservations
```
**Request:**
```json
{
  "pointId": 1,
  "date": "2024-01-15T00:00:00Z",
  "hour": 14
}
```

## Workflow đặt chỗ mới

### Bước 1: Tìm trạm sạc phù hợp
1. Người dùng cung cấp:
   - **Loại cổng sạc của xe** (`connectorType`) - tự chọn (CCS, CHAdeMO, Type2, etc.)
   - Ngày muốn đặt chỗ (`date`)
   - Vị trí hiện tại (`latitude`, `longitude`) - tùy chọn
   - Bán kính tìm kiếm (`radiusKm`) - mặc định 10km

2. Hệ thống trả về:
   - Danh sách trạm sạc có điểm sạc phù hợp
   - Sắp xếp theo khoảng cách (nếu có tọa độ)
   - Số lượng điểm sạc phù hợp tại mỗi trạm

### Bước 2: Chọn điểm sạc
1. Người dùng chọn trạm sạc từ danh sách
2. Gọi API để lấy danh sách điểm sạc phù hợp tại trạm đó
3. Chọn điểm sạc bất kỳ (miễn là có cổng phù hợp)

### Bước 3: Chọn khung giờ
1. Gọi API để lấy 24 khung giờ trong ngày đã chọn
2. Mỗi khung giờ hiển thị:
   - Giờ bắt đầu và kết thúc
   - Trạng thái có sẵn hay không
   - Số điểm sạc còn trống
3. Chọn khung giờ phù hợp

### Bước 4: Tạo đặt chỗ
1. Gửi request với:
   - `pointId`: ID điểm sạc đã chọn
   - `date`: Ngày đặt chỗ
   - `hour`: Giờ bắt đầu (0-23)
2. Hệ thống tự động tính `StartTime` và `EndTime`

## Tính năng bổ sung

### Tương thích ngược
- API cũ vẫn hoạt động với `LegacyStartTime` và `DurationMinutes`
- Không ảnh hưởng đến các đặt chỗ hiện có

### Validation
- Kiểm tra điểm sạc có tồn tại
- Kiểm tra khung giờ không bị trùng
- Kiểm tra không đặt trong quá khứ
- Kiểm tra loại cổng sạc phù hợp

### Tối ưu hóa
- Tìm kiếm theo khoảng cách địa lý
- Giới hạn số lượng kết quả
- Cache thông tin trạm sạc

## Cách sử dụng

Xem file `test_reservation_new_logic.http` để có ví dụ chi tiết về cách gọi các API mới.

## Lưu ý

1. **ConnectorType**: Người dùng tự chọn loại cổng sạc khi tìm kiếm, không cần lưu trong DriverProfile
2. **Timezone**: Tất cả thời gian được lưu dưới dạng UTC
3. **Duration**: Mỗi đặt chỗ cố định 1 tiếng
4. **Availability**: Khung giờ được kiểm tra real-time
5. **Flexibility**: Người dùng có thể thay đổi loại cổng sạc mỗi lần đặt chỗ
