# OAuth Implementation Guide - Google & Facebook Login

## 📋 Tổng quan

Hệ thống đã được tích hợp OAuth để cho phép đăng nhập/đăng ký bằng tài khoản Google hoặc Facebook.

## ✅ Những gì đã được implement

### 1. Database Schema
- Thêm các field OAuth vào bảng `User`:
  - `provider` (NVARCHAR(50)): Loại provider ("google", "facebook", etc.)
  - `provider_id` (NVARCHAR(255)): ID từ provider bên ngoài
  - `email_verified` (BIT): Trạng thái xác thực email

File SQL: `add_oauth_fields.sql`

### 2. Entity & Model
- `User.cs`: Thêm properties cho OAuth
- `OAuthLoginRequest.cs`: DTO để nhận request từ client

### 3. Service Layer
- `IAuthService`: Thêm method `OAuthLoginOrRegisterAsync()`
- `AuthService`: Implement logic đăng nhập/đăng ký OAuth

### 4. Controller
- `AuthController`: Endpoint `/api/auth/oauth/login`

## 🚀 Cách sử dụng API

### Endpoint: `POST /api/auth/oauth/login`

**Request Body:**
```json
{
  "provider": "google",
  "providerId": "1234567890",
  "email": "user@gmail.com",
  "name": "Nguyen Van A",
  "phone": "0123456789",
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
    "name": "Nguyen Van A",
    "email": "user@gmail.com",
    "phone": "0123456789",
    "role": "driver",
    "walletBalance": 0,
    "billingType": "postpaid",
    "membershipTier": "standard",
    "createdAt": "2025-10-27T10:00:00Z"
  }
}
```

## 🔄 Luồng hoạt động

### Trường hợp 1: User mới (Đăng ký tự động)
1. Frontend gửi thông tin từ Google/Facebook SDK
2. Backend kiểm tra chưa có tài khoản → **Tạo tài khoản mới**
3. Set `email_verified = true` (vì OAuth provider đã verify email)
4. Trả về JWT token

### Trường hợp 2: User đã tồn tại (Đăng nhập)
1. Frontend gửi thông tin từ OAuth SDK
2. Backend tìm theo `provider + provider_id`
3. Nếu tìm thấy → Trả về JWT token
4. Nếu không tìm thấy → Chuyển sang đăng ký

### Trường hợp 3: Conflict (Email đã tồn tại)
- Nếu email đã được đăng ký bằng email/password thường
- API sẽ trả về lỗi: `"Email user@gmail.com is already registered with a different account"`

## 🎯 Tích hợp với Frontend

### Google Sign-In

1. **Thêm Google Sign-In SDK vào HTML:**
```html
<script src="https://accounts.google.com/gsi/client" async defer></script>
```

2. **Initialize Google Sign-In:**
```javascript
window.onload = function () {
  google.accounts.id.initialize({
    client_id: 'YOUR_GOOGLE_CLIENT_ID',
    callback: handleGoogleResponse
  });
  
  google.accounts.id.renderButton(
    document.getElementById('google-signin-button'),
    { theme: 'outline', size: 'large' }
  );
}
```

3. **Handle response:**
```javascript
async function handleGoogleResponse(response) {
  // Decode the JWT token from Google
  const payload = JSON.parse(atob(response.credential.split('.')[1]));
  
  // Send to your backend
  const oauthData = {
    provider: 'google',
    providerId: payload.sub,
    email: payload.email,
    name: payload.name,
    phone: null,  // Google doesn't provide phone
    role: 'driver'
  };
  
  const result = await fetch('https://localhost:7035/api/auth/oauth/login', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(oauthData)
  });
  
  const data = await result.json();
  if (result.ok) {
    // Store token
    localStorage.setItem('token', data.token);
    // Redirect to dashboard
    window.location.href = '/dashboard';
  }
}
```

### Facebook Login

1. **Thêm Facebook SDK:**
```html
<script async defer crossorigin="anonymous" 
  src="https://connect.facebook.net/en_US/sdk.js"></script>
```

2. **Initialize Facebook SDK:**
```javascript
window.fbAsyncInit = function() {
  FB.init({
    appId: 'YOUR_FACEBOOK_APP_ID',
    cookie: true,
    xfbml: true,
    version: 'v18.0'
  });
};
```

3. **Login handler:**
```javascript
FB.login(function(response) {
  if (response.authResponse) {
    // Get user info
    FB.api('/me', { fields: 'id,name,email' }, async function(user) {
      const oauthData = {
        provider: 'facebook',
        providerId: user.id,
        email: user.email,
        name: user.name,
        phone: null,
        role: 'driver'
      };
      
      const result = await fetch('https://localhost:7035/api/auth/oauth/login', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(oauthData)
      });
      
      const data = await result.json();
      if (result.ok) {
        localStorage.setItem('token', data.token);
        window.location.href = '/dashboard';
      }
    });
  }
}, { scope: 'email' });
```

## 🔐 Security Features

1. **No Password for OAuth Users**
   - OAuth users không cần password
   - Backend tự tạo random password

2. **Email Verified**
   - `email_verified = true` cho tất cả OAuth users
   - Vì Google/Facebook đã verify email

3. **Provider + ProviderId Validation**
   - Kiểm tra `provider` phải hợp lệ
   - `provider_id` là unique identifier từ provider

4. **Conflict Detection**
   - Ngăn người dùng đăng ký email đã tồn tại
   - Bảo vệ user khỏi account take-over

## 📝 Lưu ý quan trọng

### Database Migration
Chạy SQL script để cập nhật schema:
```bash
sqlcmd -S .\SQLEXPRESS -d EVChargingManagement -i add_oauth_fields.sql
```

### Client Credentials
Để get Google/Facebook credentials:
1. **Google**: https://console.cloud.google.com/
2. **Facebook**: https://developers.facebook.com/apps/

### Testing

**Test với Postman/REST Client:**

```http
POST https://localhost:7035/api/auth/oauth/login
Content-Type: application/json

{
  "provider": "google",
  "providerId": "test_123456",
  "email": "test@gmail.com",
  "name": "Test User",
  "role": "driver"
}
```

## 🔮 Future Enhancements

Có thể mở rộng thêm:
1. **Link multiple providers** vào 1 account
2. **OAuth callback URLs** thay vì manual API calls
3. **Apple Sign-In** support
4. **Refresh tokens** cho OAuth
5. **Profile picture** sync từ provider

## 📚 Files đã thay đổi

```
✅ EVCharging.BE.DAL/Entities/User.cs - Added OAuth fields
✅ EVCharging.BE.Common/DTOs/Auth/OAuthLoginRequest.cs - New DTO
✅ EVCharging.BE.Services/Services/Auth/IAuthService.cs - Added method
✅ EVCharging.BE.Services/Services/Auth/Implementations/AuthService.cs - Implemented
✅ EVCharging.BE.API/Controllers/AuthController.cs - Added endpoint
✅ EVCharging.BE.API/EVCharging.BE.API.csproj - Added NuGet packages
✅ add_oauth_fields.sql - Database migration script
```

## ✅ Summary

**Câu trả lời cho câu hỏi của bạn:**
> "Nếu đăng nhập bằng FB hay Google thì phải làm sao?"

✅ **Giải pháp**: 
- **Đăng ký TỰ ĐỘNG** khi lần đầu đăng nhập bằng OAuth
- **Đăng nhập tự động** nếu đã có tài khoản
- **Không cần password** cho OAuth users
- **Email tự động verified** 

API endpoint: `/api/auth/oauth/login` - Frontend chỉ cần call API này với thông tin từ Google/Facebook SDK!

