# Test Script cho Auth Fix

## Vấn đề đã sửa:

### 1. **Lỗi đăng ký tài khoản trùng email**
- **Nguyên nhân**: Logic cũ có thể tạo user trước khi check unique constraint
- **Giải pháp**: 
  - Check email trước khi bắt đầu transaction
  - Sử dụng `AsNoTracking()` cho việc check để tối ưu performance
  - Thêm exception handling cho unique constraint violation
  - Cải thiện transaction management

### 2. **Lỗi đăng nhập**
- **Nguyên nhân**: Có thể do password hashing hoặc token generation
- **Giải pháp**: 
  - Đã sử dụng BCrypt cho password hashing (an toàn hơn)
  - Cải thiện error handling và logging
  - Thêm validation cho email format

## Các thay đổi chính:

### AuthService.cs:
```csharp
// ✅ CHECK EXISTING USER TRƯỚC - KHÔNG DÙNG TRANSACTION CHO CHECK
var existingUser = await _db.Users
    .AsNoTracking()
    .FirstOrDefaultAsync(u => u.Email == request.Email);

if (existingUser != null)
{
    Console.WriteLine($"❌ User already exists: {request.Email}");
    return null;
}

// ✅ SỬ DỤNG TRANSACTION CHO VIỆC TẠO USER
using var transaction = await _db.Database.BeginTransactionAsync();

// ✅ CHECK NẾU LÀ UNIQUE CONSTRAINT VIOLATION
if (ex.InnerException?.Message.Contains("UNIQUE constraint") == true || 
    ex.Message.Contains("UNIQUE constraint") ||
    ex.InnerException?.Message.Contains("duplicate key") == true ||
    ex.Message.Contains("duplicate key"))
{
    Console.WriteLine($"❌ Email already exists (caught by constraint): {request.Email}");
    return null;
}
```

### AuthController.cs:
```csharp
// ✅ VALIDATE EMAIL FORMAT
if (!IsValidEmail(request.Email))
{
    return BadRequest(new { 
        message = "Invalid email format",
        code = "INVALID_EMAIL"
    });
}

// ✅ VALIDATE PASSWORD STRENGTH
if (request.Password.Length < 6)
{
    return BadRequest(new { 
        message = "Password must be at least 6 characters long",
        code = "WEAK_PASSWORD"
    });
}

// ✅ RETURN CONFLICT STATUS FOR DUPLICATE EMAIL
return Conflict(new { 
    message = "User with this email already exists",
    code = "EMAIL_EXISTS"
});
```

## Test Cases:

### 1. Test đăng ký với email mới:
```bash
POST /api/auth/register
{
    "name": "Test User",
    "email": "test@example.com",
    "password": "password123",
    "phone": "0123456789",
    "role": "driver"
}
```
**Expected**: 201 Created với token và user data

### 2. Test đăng ký với email đã tồn tại:
```bash
POST /api/auth/register
 
```
**Expected**: 409 Conflict với message "User with this email already exists"

### 3. Test đăng nhập với email đã đăng ký:
```bash
POST /api/auth/login
{
    "email": "test@example.com",
    "password": "password123"
}
```
**Expected**: 200 OK với token và user data

### 4. Test đăng nhập với email không tồn tại:
```bash
POST /api/auth/login
{
    "email": "nonexistent@example.com",
    "password": "password123"
}
```
**Expected**: 401 Unauthorized với message "Invalid email or password"

### 5. Test đăng nhập với password sai:
```bash
POST /api/auth/login
{
    "email": "test@example.com",
    "password": "wrongpassword"
}
```
**Expected**: 401 Unauthorized với message "Invalid email or password"

## Database Constraints:
- Email có unique constraint: `UQ__User__AB6E6164DB60277F`
- Đảm bảo không thể tạo 2 user với cùng email

## Logging:
- Tất cả các bước đều có console logging để debug
- Sử dụng emoji để dễ đọc: ✅ (success), ❌ (error), 🔸 (info)