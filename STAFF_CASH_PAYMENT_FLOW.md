# Luồng Thanh Toán Tiền Mặt của Staff

## Tổng Quan

Khi khách hàng walk-in (không có app) đến trạm và thanh toán bằng tiền mặt, staff cần thực hiện các bước sau:

---

## Luồng Chi Tiết

### **Bước 1: Staff Tạo Walk-In Session** ✅ (Đã có)

**Endpoint:** `POST /api/staff/charging/walk-in/start`

**Request Body:**
```json
{
  "chargingPointId": 1,
  "customerName": "Nguyễn Văn A",
  "customerPhone": "0901234567",
  "vehiclePlate": "30A-12345",
  "vehicleModel": "Tesla Model 3",
  "initialSOC": 20,
  "targetSOC": 80,
  "paymentMethod": "cash",  // ← Chọn "cash"
  "batteryCapacity": 75,
  "notes": "Khách hàng lần đầu"
}
```

**Kết quả:**
- Session được tạo với `status = "in_progress"`
- `FinalCost = 0` (chưa tính)
- Payment chưa được tạo

---

### **Bước 2: Session Hoàn Thành** ✅ (Đã có)

**Khi nào:**
- Driver tự dừng (qua app)
- Staff dừng session (qua endpoint dừng thường)
- Staff emergency stop (trường hợp đặc biệt)

**Điều gì xảy ra:**
- `ChargingService.StopSessionAsync()` được gọi
- Tính toán `FinalCost` dựa trên:
  - `EnergyUsed` (kWh)
  - `DurationMinutes`
  - `PricePerKwh` của charging point
  - Discount (nếu có)
- `Session.Status = "completed"`
- `Session.FinalCost` được cập nhật

**Ví dụ kết quả:**
```json
{
  "sessionId": 5,
  "finalCost": 150000,  // 150,000 VND
  "energyUsed": 30.5,   // 30.5 kWh
  "status": "completed"
}
```

---

### **Bước 3: Staff Tạo Payment Record** ❌ (CHƯA CÓ - CẦN THÊM)

**Endpoint mới cần tạo:** `POST /api/staff/charging/sessions/{sessionId}/create-payment`

**Mục đích:**
- Tạo Payment record trong database
- Lưu thông tin thanh toán tiền mặt
- Link Payment với Session

**Request:**
```json
{
  "paymentMethod": "cash",  // "cash", "card", hoặc "pos"
  "notes": "Khách hàng đã nhận hóa đơn"
}
```

**Logic trong Service:**
```csharp
// 1. Kiểm tra session tồn tại và thuộc trạm của staff
// 2. Kiểm tra session đã completed chưa
// 3. Kiểm tra FinalCost > 0
// 4. Kiểm tra payment chưa tồn tại cho session này
// 5. Tạo Payment record:
//    - UserId = guestDriver.UserId (hoặc null cho walk-in)
//    - SessionId = sessionId
//    - Amount = session.FinalCost
//    - PaymentMethod = "cash"
//    - PaymentStatus = "pending"
//    - PaymentType = "session_payment"
// 6. Log staff action
```

**Response:**
```json
{
  "message": "Payment created successfully",
  "data": {
    "paymentId": 10,
    "sessionId": 5,
    "amount": 150000,
    "paymentMethod": "cash",
    "paymentStatus": "pending",
    "invoiceNumber": "INV-20250130-0010",
    "createdAt": "2025-01-30T10:30:00Z"
  }
}
```

---

### **Bước 4: Khách Hàng Trả Tiền Mặt** ✅ (Đã có - nhưng cần điều chỉnh)

**Khi nào:**
- Khách hàng đưa tiền mặt cho staff
- Staff xác nhận đã nhận tiền

**Endpoint hiện có:** `PUT /api/payments/{paymentId}/status`

**Request:**
```json
{
  "status": "completed",
  "transactionId": null  // null cho tiền mặt
}
```

**Logic:**
- Update `Payment.PaymentStatus = "completed"`
- Log thời gian thanh toán
- Có thể tạo invoice (bước 5)

**Response:**
```json
{
  "message": "Payment status updated successfully",
  "data": {
    "paymentId": 10,
    "paymentStatus": "completed",
    "completedAt": "2025-01-30T10:35:00Z"
  }
}
```

**⚠️ Lưu ý:** 
- Endpoint này hiện yêu cầu `[Authorize(Roles = "Admin,Staff")]` - cần đảm bảo Staff có quyền
- Cần thêm validation để Staff chỉ update payment của session thuộc trạm của họ

---

### **Bước 5: Tạo Hóa Đơn (Tùy chọn)** ✅ (Đã có)

**Endpoint:** `POST /api/payments/{paymentId}/invoice`

**Khi nào:**
- Khách hàng yêu cầu hóa đơn
- Bắt buộc nếu khách hàng là doanh nghiệp

**Response:**
```json
{
  "message": "Invoice generated successfully",
  "data": {
    "invoiceNumber": "INV-20250130-0010",
    "invoiceUrl": "https://api.example.com/invoices/INV-20250130-0010.pdf"
  }
}
```

---

## Tổng Kết Endpoints

| Bước | Endpoint | Method | Status |
|------|----------|--------|--------|
| 1. Tạo session | `/api/staff/charging/walk-in/start` | POST | ✅ Đã có |
| 2. Session hoàn thành | `/api/charging-sessions/stop` | POST | ✅ Đã có |
| 3. Tạo payment | `/api/staff/charging/sessions/{sessionId}/create-payment` | POST | ❌ **CẦN THÊM** |
| 4. Xác nhận thanh toán | `/api/payments/{paymentId}/status` | PUT | ✅ Đã có (cần điều chỉnh) |
| 5. Tạo hóa đơn | `/api/payments/{paymentId}/invoice` | POST | ✅ Đã có |

---

## Các Trường Hợp Đặc Biệt

### 1. **Khách hàng thanh toán trước khi session kết thúc**
- Cho phép tạo payment với `Amount` = estimated cost
- Khi session kết thúc, cập nhật `Amount` = actual `FinalCost`
- Nếu thừa: tạo refund hoặc ghi chú
- Nếu thiếu: yêu cầu thanh toán bổ sung

### 2. **Khách hàng không có tiền mặt đủ**
- Tạo payment với `PaymentStatus = "pending"`
- Cho phép thanh toán sau (cần thu thập thông tin liên lạc)
- Có thể chuyển sang phương thức khác (card/pos)

### 3. **Session bị emergency stop**
- Vẫn tính cost cho phần năng lượng đã sạc
- Tạo payment với `Amount` = partial cost
- Có thể kèm theo incident report

---

## Cần Implement

### 1. **DTO: `StaffCreatePaymentRequest.cs`**
```csharp
public class StaffCreatePaymentRequest
{
    [Required]
    [RegularExpression("^(cash|card|pos)$")]
    public string PaymentMethod { get; set; } = "cash";
    
    [StringLength(500)]
    public string? Notes { get; set; }
}
```

### 2. **Service Method: `IStaffChargingService.CreatePaymentForSessionAsync()`**
```csharp
Task<PaymentResponse?> CreatePaymentForSessionAsync(
    int staffId, 
    int sessionId, 
    StaffCreatePaymentRequest request);
```

### 3. **Controller Endpoint trong `StaffChargingController`**
```csharp
[HttpPost("sessions/{sessionId}/create-payment")]
public async Task<IActionResult> CreatePaymentForSession(
    int sessionId, 
    [FromBody] StaffCreatePaymentRequest request)
```

### 4. **Cải thiện `UpdatePaymentStatusAsync`**
- Thêm validation: Staff chỉ update payment của session thuộc trạm của họ
- Kiểm tra `PaymentMethod = "cash"` khi update từ "pending" → "completed"

---

## Database Schema

**Payment Table:**
- `payment_id` (PK)
- `user_id` (FK) - có thể null cho walk-in
- `session_id` (FK) - **REQUIRED** cho cash payment
- `amount` - Số tiền
- `payment_method` - "cash", "card", "pos", "wallet", etc.
- `payment_status` - "pending", "completed", "failed", "refunded"
- `payment_type` - "session_payment", "deposit", "top_up", "refund"
- `invoice_number` - Số hóa đơn
- `created_at` - Thời gian tạo

---

## Security & Validation

1. **Authorization:**
   - Chỉ Staff đã được assign vào trạm mới có thể tạo payment
   - Staff chỉ update payment của session thuộc trạm của họ

2. **Validation:**
   - Session phải tồn tại và `status = "completed"`
   - `FinalCost > 0`
   - Chưa có payment nào cho session này (hoặc cho phép tạo lại nếu payment failed)

3. **Audit:**
   - Log mọi thao tác của Staff với payment
   - Ghi lại `staffId`, `action`, `timestamp`

