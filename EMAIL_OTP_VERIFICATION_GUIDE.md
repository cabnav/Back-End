# Email OTP Verification Guide

## 📋 Tổng quan

Hệ thống đã được tích hợp **Email OTP (One-Time Password)** để xác thực email khi đăng ký tài khoản. Người dùng phải nhập email → nhận mã OTP qua email → nhập mã OTP để hoàn tất đăng ký.

## 🔐 Tính năng

- ✅ **OTP tự động hết hạn sau 30 phút**
- ✅ **Mã OTP 6 chữ số ngẫu nhiên**
- ✅ **Tự động vô hiệu OTP cũ khi gửi OTP mới**
- ✅ **Chỉ được sử dụng 1 lần**
- ✅ **Kiểm tra email đã tồn tại**
- ✅ **Email HTML đẹp mắt**

## 🗄️ Database Schema

### Bảng EmailOTP

```sql
CREATE TABLE EmailOTP (
    otp_id INT IDENTITY(1,1) PRIMARY KEY,
    email NVARCHAR(255) NOT NULL,
    otp_code NVARCHAR(6) NOT NULL,
    created_at DATETIME2 NOT NULL DEFAULT GETDATE(),
    expires_at DATETIME2 NOT NULL,
    is_used BIT NOT NULL DEFAULT 0,
    purpose NVARCHAR(50) DEFAULT 'registration'
);
```

**Chạy script SQL:**
```bash
sqlcmd -S .\SQLEXPRESS -d EVChargingManagement -i create_email_otp_table.sql
```

## 📡 API Endpoints

### 1. Send OTP to Email

**Endpoint:** `POST /api/auth/send-otp`

**Request Body:**
```json
{
  "email": "user@example.com"
}
```

**Response (Success):**
```json
{
  "message": "OTP sent successfully to your email. Please check your inbox."
}
```

**Response (Error - Email already registered):**
```json
{
  "message": "Failed to send OTP. Email may already be registered."
}
```

### 2. Verify OTP Code

**Endpoint:** `POST /api/auth/verify-otp`

**Request Body:**
```json
{
  "email": "user@example.com",
  "otpCode": "123456"
}
```

**Response (Success):**
```json
{
  "message": "OTP verified successfully"
}
```

**Response (Error):**
```json
{
  "message": "Invalid or expired OTP code"
}
```

### 3. Register with OTP

**Endpoint:** `POST /api/auth/register`

**Request Body:**
```json
{
  "name": "Test User",
  "email": "user@example.com",
  "password": "password123",
  "phone": "0123456789",
  "otpCode": "123456",
  "role": "driver",
  "licenseNumber": "A123456",
  "vehicleModel": "Tesla Model 3",
  "vehiclePlate": "29A-12345",
  "batteryCapacity": 75
}
```

**Response (Success):**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresAt": "2025-10-28T10:00:00Z",
  "user": {
    "userId": 1,
    "name": "Test User",
    "email": "user@example.com",
    "phone": "0123456789",
    "role": "driver",
    "walletBalance": 0,
    "billingType": "postpaid",
    "membershipTier": "standard",
    "createdAt": "2025-10-27T10:00:00Z"
  }
}
```

**Response (Error - Invalid/Expired OTP):**
```json
{
  "message": "Invalid or expired OTP code. Please request a new OTP."
}
```

## 🔄 Luồng hoạt động

### Flow đăng ký với OTP:

```
┌─────────────┐
│  User nhập  │
│   email     │
└─────┬───────┘
      │
      ▼
┌─────────────────────────────────┐
│  POST /api/auth/send-otp        │
│  - Generate 6-digit OTP         │
│  - Set expiry: 30 minutes       │
│  - Invalidate old OTPs          │
│  - Send email                   │
└─────┬───────────────────────────┘
      │
      ▼
┌─────────────────────────────────┐
│  User check email inbox         │
│  Receive OTP: 123456            │
└─────┬───────────────────────────┘
      │
      ▼
┌─────────────────────────────────┐
│  User nhập thông tin + OTP      │
│  POST /api/auth/register        │
└─────┬───────────────────────────┘
      │
      ▼
┌─────────────────────────────────┐
│  Backend verify OTP             │
│  - Check exists, not used       │
│  - Check not expired            │
│  - Mark as used                 │
│  - Create user account          │
│  - Return JWT token             │
└─────────────────────────────────┘
```

## 📝 Chi tiết implementation

### 1. EmailOTP Entity
**File:** `EVCharging.BE.DAL/Entities/EmailOTP.cs`
```csharp
public partial class EmailOTP
{
    public int OtpId { get; set; }
    public string Email { get; set; } = null!;
    public string OtpCode { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; }
    public string? Purpose { get; set; }
}
```

### 2. EmailOTPService
**File:** `EVCharging.BE.Services/Services/Auth/Implementations/EmailOTPService.cs`

**Key Methods:**
- `SendOTPAsync()`: Generate và gửi OTP
- `VerifyOTPAsync()`: Xác thực OTP
- `HasValidOTPAsync()`: Check có OTP hợp lệ
- `DeleteExpiredOTPsAsync()`: Cleanup expired OTPs

**Logic:**
1. Check email đã tồn tại chưa
2. Generate random 6-digit OTP (100000-999999)
3. Set expiry: current time + 30 minutes
4. Invalidate tất cả OTP cũ của email đó
5. Save vào database
6. Gửi email HTML đẹp

### 3. RegisterRequest DTO
**File:** `EVCharging.BE.Common/DTOs/Auth/RegisterRequest.cs`

**Thêm field:**
```csharp
[Required(ErrorMessage = "OTP code is required")]
[StringLength(6, MinimumLength = 6, ErrorMessage = "OTP code must be 6 digits")]
public string OtpCode { get; set; }
```

### 4. AuthService
**File:** `EVCharging.BE.Services/Services/Auth/Implementations/AuthService.cs`

**Đã thêm OTP verification vào RegisterAsync:**
```csharp
// Validate and verify OTP
var isOtpValid = await _emailOTPService.VerifyOTPAsync(request.Email, request.OtpCode);
if (!isOtpValid)
    throw new InvalidOperationException("Invalid or expired OTP code. Please request a new OTP.");
```

## 🧪 Testing

### Test Case 1: Complete Flow

1. **Send OTP:**
```bash
POST http://localhost:7035/api/auth/send-otp
Content-Type: application/json

{
  "email": "test@example.com"
}
```

2. **Check email inbox** → Nhận mã OTP (ví dụ: 123456)

3. **Register with OTP:**
```bash
POST http://localhost:7035/api/auth/register
Content-Type: application/json

{
  "name": "Test User",
  "email": "test@example.com",
  "password": "password123",
  "phone": "0123456789",
  "otpCode": "123456",
  "role": "driver"
}
```

### Test Case 2: Invalid OTP

```bash
POST http://localhost:7035/api/auth/register
Content-Type: application/json

{
  "name": "Test User",
  "email": "test@example.com",
  "password": "password123",
  "phone": "0123456789",
  "otpCode": "000000",
  "role": "driver"
}
```

**Expected:** `400 Bad Request - "Invalid or expired OTP code. Please request a new OTP."`

### Test Case 3: Expired OTP

1. Send OTP
2. Wait 31 minutes
3. Try register

**Expected:** `400 Bad Request - "Invalid or expired OTP code. Please request a new OTP."`

### Test Case 4: Reuse OTP

1. Send OTP
2. Register successfully với OTP
3. Try register again với cùng OTP

**Expected:** `400 Bad Request - "Invalid or expired OTP code"` (vì đã mark is_used = true)

## 📧 Email Template

Email OTP được gửi dạng **HTML** với:
- ✅ Font Arial, color scheme đẹp
- ✅ Mã OTP lớn, dễ đọc
- ✅ Lưu ý về expiry (30 phút)
- ✅ Security warnings
- ✅ Responsive design

**Preview:**
```
╔══════════════════════════════════╗
║  Mã xác nhận email               ║
║                                  ║
║  Xin chào,                       ║
║                                  ║
║  Chúng tôi nhận được yêu cầu     ║
║  xác nhận email user@gmail.com.  ║
║                                  ║
║  ┌────────────────────────────┐  ║
║  │ Mã xác nhận của bạn:      │  ║
║  │                            │  ║
║  │     1  2  3  4  5  6       │  ║
║  │                            │  ║
║  └────────────────────────────┘  ║
║                                  ║
║  Lưu ý:                         ║
║  - Mã hết hạn sau 30 phút       ║
║  - Không chia sẻ mã với ai      ║
║                                  ║
╚══════════════════════════════════╝
```

## 🔧 Configuration

### Email Settings (appsettings.json)

```json
{
  "Email": {
    "SmtpHost": "smtp.gmail.com",
    "SmtpPort": "587",
    "SmtpUsername": "your_email@gmail.com",
    "SmtpPassword": "your_app_password",
    "FromEmail": "your_email@gmail.com",
    "FromName": "EV Charging System"
  }
}
```

**Note:** Gmail cần **App Password** chứ không phải password thường.

## 🗑️ Cleanup Expired OTPs

Có thể tạo background job để cleanup expired OTPs:

```csharp
// Chạy mỗi 1 giờ
public class CleanupExpiredOTPsService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var deleted = await _emailOTPService.DeleteExpiredOTPsAsync();
            Console.WriteLine($"Deleted {deleted} expired OTPs");
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}
```

## 🔒 Security Features

✅ **OTP ngẫu nhiên** (không sequential)  
✅ **Expiry: 30 phút**  
✅ **Single-use** (is_used)  
✅ **Email uniqueness** check  
✅ **Invalidate old OTPs** khi gửi mới  
✅ **Email HTML template**  
✅ **Valid email format** check  

## 📚 Files Changed

```
✅ create_email_otp_table.sql - SQL script
✅ EVCharging.BE.DAL/Entities/EmailOTP.cs - Entity
✅ EVCharging.BE.DAL/EvchargingManagementContext.cs - DBContext
✅ EVCharging.BE.Common/DTOs/Auth/RegisterRequest.cs - Added OtpCode
✅ EVCharging.BE.Common/DTOs/Auth/SendOTPRequest.cs - New DTOs
✅ EVCharging.BE.Services/Services/Auth/IEmailOTPService.cs - Interface
✅ EVCharging.BE.Services/Services/Auth/Implementations/EmailOTPService.cs - Implementation
✅ EVCharging.BE.Services/Services/Auth/Implementations/AuthService.cs - Added OTP verification
✅ EVCharging.BE.API/Controllers/AuthController.cs - Added endpoints
✅ EVCharging.BE.API/Program.cs - Registered service
✅ test_register.http - Updated test cases
```

## ✅ Summary

**Đáp ứng yêu cầu:**
- ✅ Người dùng nhập email → Gửi OTP về email
- ✅ OTP 6 chữ số
- ✅ Hết hạn sau 30 phút
- ✅ Phải nhập đúng OTP mới đăng ký được
- ✅ Không có migration (Database-first approach với SQL script)

**Endpoints:**
1. `POST /api/auth/send-otp` - Gửi OTP
2. `POST /api/auth/verify-otp` - Xác thực OTP
3. `POST /api/auth/register` - Đăng ký (require OTP)

**Flow:**
```
User → Send Email → Receive OTP → Enter OTP + Info → Register Success
```

**Lưu ý:** Nhớ chạy SQL script `create_email_otp_table.sql` trước khi test!

