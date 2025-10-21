# 💳 EV Charging Payment System - Hướng dẫn sử dụng

## 📋 **Tổng quan**

Payment System đã được implement hoàn chỉnh với các tính năng:

- ✅ **Core Payment Operations** - Quản lý thanh toán cơ bản
- ✅ **Wallet Integration** - Tích hợp ví điện tử
- ✅ **VNPay Integration** - Tích hợp VNPay payment gateway
- ✅ **MoMo Integration** - Tích hợp MoMo payment gateway
- ✅ **Refund System** - Hệ thống hoàn tiền
- ✅ **Invoice Generation** - Tạo hóa đơn điện tử
- ✅ **Payment Analytics** - Phân tích thanh toán (Admin)
- ✅ **Real-time Notifications** - Thông báo real-time

## 🚀 **API Endpoints**

### **1. Core Payment Operations**

#### **Tạo payment mới**
```http
POST /api/payments
Authorization: Bearer {token}
Content-Type: application/json

{
  "sessionId": 1,
  "amount": 50000,
  "paymentMethod": "wallet",
  "description": "Payment for charging session"
}
```

#### **Lấy thông tin payment**
```http
GET /api/payments/{id}
Authorization: Bearer {token}
```

#### **Lấy danh sách payments của user**
```http
GET /api/payments/my-payments?page=1&pageSize=50
Authorization: Bearer {token}
```

### **2. Payment Gateway Integration**

#### **VNPay Payment**
```http
POST /api/payments/vnpay
Authorization: Bearer {token}
Content-Type: application/json

{
  "sessionId": 1,
  "amount": 100000,
  "description": "VNPay payment for charging",
  "returnUrl": "https://yourapp.com/payment/success"
}
```

#### **MoMo Payment**
```http
POST /api/payments/momo
Authorization: Bearer {token}
Content-Type: application/json

{
  "sessionId": 1,
  "amount": 75000,
  "description": "MoMo payment for charging",
  "returnUrl": "https://yourapp.com/payment/success"
}
```

#### **Wallet Payment**
```http
POST /api/payments/wallet
Authorization: Bearer {token}
Content-Type: application/json

{
  "sessionId": 1,
  "amount": 30000,
  "description": "Wallet payment for charging"
}
```

### **3. Payment Callbacks**

#### **VNPay Callback**
```http
POST /api/payments/callback/vnpay
Content-Type: application/json

{
  "TransactionId": "TXN123456789",
  "PaymentId": "1",
  "Status": "00",
  "Amount": 100000,
  "Signature": "abc123def456",
  "Message": "Success"
}
```

#### **MoMo Callback**
```http
POST /api/payments/callback/momo
Content-Type: application/json

{
  "TransactionId": "MOMO123456789",
  "PaymentId": "1",
  "Status": "00",
  "Amount": 75000,
  "Signature": "momo123def456",
  "Message": "Success"
}
```

### **4. Refund Operations**

#### **Tạo yêu cầu hoàn tiền**
```http
POST /api/payments/refund
Authorization: Bearer {token}
Content-Type: application/json

{
  "paymentId": 1,
  "amount": 25000,
  "reason": "Session cancelled by user"
}
```

### **5. Invoice Operations**

#### **Tạo hóa đơn điện tử**
```http
POST /api/payments/{paymentId}/invoice
Authorization: Bearer {token}
```

### **6. Analytics (Admin Only)**

#### **Payment Analytics**
```http
GET /api/payments/analytics?from=2023-01-01&to=2023-12-31
Authorization: Bearer {adminToken}
```

#### **Total Revenue**
```http
GET /api/payments/revenue?from=2023-01-01&to=2023-12-31
Authorization: Bearer {adminToken}
```

#### **Payment Method Statistics**
```http
GET /api/payments/payment-methods-stats?from=2023-01-01&to=2023-12-31
Authorization: Bearer {adminToken}
```

## ⚙️ **Cấu hình**

### **1. VNPay Configuration**
```json
{
  "VNPay": {
    "TmnCode": "YOUR_TMN_CODE",
    "HashSecret": "YOUR_HASH_SECRET",
    "BaseUrl": "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html",
    "ReturnUrl": "https://localhost:7035/payment/return",
    "CancelUrl": "https://localhost:7035/payment/cancel",
    "NotifyUrl": "https://localhost:7035/api/payments/callback/vnpay"
  }
}
```

### **2. MoMo Configuration**
```json
{
  "MoMo": {
    "PartnerCode": "YOUR_PARTNER_CODE",
    "AccessKey": "YOUR_ACCESS_KEY",
    "SecretKey": "YOUR_SECRET_KEY",
    "BaseUrl": "https://test-payment.momo.vn/v2/gateway/pay",
    "ReturnUrl": "https://localhost:7035/payment/return",
    "NotifyUrl": "https://localhost:7035/api/payments/callback/momo"
  }
}
```

## 🔧 **Services Architecture**

### **Core Services**
- **IPaymentService** - Main payment business logic
- **IVNPayService** - VNPay gateway integration
- **IMoMoService** - MoMo gateway integration

### **DTOs**
- **PaymentCreateRequest** - Tạo payment mới
- **PaymentResponse** - Response cho payment operations
- **PaymentCallbackRequest/Response** - Callback handling
- **RefundRequest/Response** - Refund operations

## 📊 **Database Schema**

### **Payment Entity**
```sql
CREATE TABLE Payments (
    PaymentId INT PRIMARY KEY IDENTITY(1,1),
    UserId INT NOT NULL,
    SessionId INT NULL,
    ReservationId INT NULL,
    Amount DECIMAL(18,2) NOT NULL,
    PaymentMethod NVARCHAR(50),
    PaymentStatus NVARCHAR(50),
    InvoiceNumber NVARCHAR(100),
    CreatedAt DATETIME2,
    FOREIGN KEY (UserId) REFERENCES Users(UserId),
    FOREIGN KEY (SessionId) REFERENCES ChargingSessions(SessionId),
    FOREIGN KEY (ReservationId) REFERENCES Reservations(ReservationId)
);
```

### **WalletTransaction Entity**
```sql
CREATE TABLE WalletTransactions (
    TransactionId INT PRIMARY KEY IDENTITY(1,1),
    UserId INT NOT NULL,
    Amount DECIMAL(18,2) NOT NULL,
    TransactionType NVARCHAR(50),
    Description NVARCHAR(500),
    BalanceAfter DECIMAL(18,2),
    ReferenceId INT NULL,
    CreatedAt DATETIME2,
    FOREIGN KEY (UserId) REFERENCES Users(UserId)
);
```

## 🔐 **Security Features**

### **1. Authentication & Authorization**
- JWT token authentication
- Role-based access control (Admin, Staff, User)
- Payment ownership validation

### **2. Payment Security**
- Signature verification for payment gateways
- Secure hash generation
- Transaction validation

### **3. Data Protection**
- Input validation
- SQL injection prevention
- XSS protection

## 🧪 **Testing**

### **Test File: PaymentsTest.http**
File `PaymentsTest.http` chứa đầy đủ test cases cho tất cả endpoints:

1. **Authentication Tests**
2. **Core Payment Operations**
3. **Payment Gateway Integration**
4. **Refund Operations**
5. **Invoice Operations**
6. **Analytics (Admin)**
7. **Error Handling**

### **Chạy Tests**
```bash
# 1. Start the API
dotnet run --project EVCharging.BE.API

# 2. Use PaymentsTest.http trong VS Code với REST Client extension
# 3. Thay thế {token} và {adminToken} bằng JWT tokens thực tế
```

## 🚨 **Error Handling**

### **Common Error Responses**
```json
{
  "message": "Error description",
  "error": "Detailed error message"
}
```

### **HTTP Status Codes**
- **200 OK** - Success
- **201 Created** - Payment created
- **400 Bad Request** - Invalid request
- **401 Unauthorized** - Authentication required
- **403 Forbidden** - Insufficient permissions
- **404 Not Found** - Payment not found
- **500 Internal Server Error** - Server error

## 📈 **Analytics & Reporting**

### **Payment Analytics Response**
```json
{
  "totalPayments": 150,
  "totalAmount": 5000000,
  "successfulPayments": 145,
  "failedPayments": 5,
  "averageAmount": 33333.33
}
```

### **Revenue Analytics**
```json
{
  "totalRevenue": 4500000,
  "period": {
    "from": "2023-01-01T00:00:00Z",
    "to": "2023-12-31T23:59:59Z"
  }
}
```

### **Payment Method Statistics**
```json
{
  "wallet": 80,
  "vnpay": 45,
  "momo": 25,
  "credit_card": 10
}
```

## 🔄 **Payment Flow**

### **1. Wallet Payment Flow**
```
User Request → Validate Balance → Deduct Wallet → Update Payment Status → Send Notification
```

### **2. VNPay Payment Flow**
```
User Request → Create VNPay URL → Redirect to VNPay → User Payment → VNPay Callback → Update Status → Send Notification
```

### **3. MoMo Payment Flow**
```
User Request → Create MoMo URL → Redirect to MoMo → User Payment → MoMo Callback → Update Status → Send Notification
```

## 🎯 **Next Steps**

### **Immediate Improvements**
1. **Invoice PDF Generation** - Tạo PDF hóa đơn
2. **Email Notifications** - Gửi email xác nhận
3. **SMS Notifications** - Gửi SMS thông báo
4. **Payment Webhooks** - Real-time updates

### **Advanced Features**
1. **Recurring Payments** - Thanh toán định kỳ
2. **Payment Plans** - Gói thanh toán
3. **Multi-currency Support** - Đa tiền tệ
4. **Fraud Detection** - Phát hiện gian lận

## 📞 **Support**

Nếu có vấn đề hoặc cần hỗ trợ, vui lòng liên hệ:
- **Email**: support@evcharging.com
- **Documentation**: [API Documentation](https://localhost:7035/swagger)
- **Test File**: `PaymentsTest.http`

---

**🎉 Payment System đã sẵn sàng sử dụng!**
