# 🏢 MoMo Developer Account - Full Guide

## 📋 TÓM TẮT

MoMo Developer Account là tài khoản chính thức cho doanh nghiệp/developer muốn tích hợp MoMo Payment Gateway vào hệ thống.

### So sánh: Test Credentials vs Developer Account

| Feature | Test Credentials | Developer Account |
|---------|-----------------|-------------------|
| **Đăng ký** | Không cần | Cần đăng ký chính thức |
| **Credentials** | Public (ai cũng dùng được) | Riêng cho bạn |
| **Environment** | Sandbox only | Sandbox + Production |
| **Thời hạn** | Vĩnh viễn (nhưng public) | Vĩnh viễn (riêng tư) |
| **Support** | Không có | Có support team |
| **Production** | ❌ Không thể | ✅ Có thể |
| **Phí** | Miễn phí | Miễn phí (phí transaction khi production) |
| **Bảo mật** | Thấp (credentials public) | Cao (credentials riêng) |

---

## 🎯 KHI NÀO NÊN DÙNG DEVELOPER ACCOUNT?

✅ **NÊN dùng khi:**
- Dự án thật, sẽ deploy lên production
- Cần credentials riêng, bảo mật
- Cần support từ MoMo team
- Doanh nghiệp/startup có giấy phép kinh doanh
- Cần tích hợp sâu hơn (refund, query transaction, etc.)

❌ **KHÔNG CẦN khi:**
- Chỉ học tập, demo
- POC (Proof of Concept)
- Dự án cá nhân, không commercial
- Thời gian ngắn, cần test nhanh

---

## 📝 BƯỚC 1: ĐĂNG KÝ MOMO DEVELOPER ACCOUNT

### 1.1. Truy cập MoMo Business Portal

🔗 **Link:** https://business.momo.vn/

### 1.2. Chọn loại tài khoản

**Option A: Doanh nghiệp (Business)**
- Có giấy phép kinh doanh
- Mã số thuế
- Công ty có pháp nhân

**Option B: Cá nhân kinh doanh (Individual Business)**
- Hộ kinh doanh cá thể
- Giấy phép kinh doanh cá nhân

**Option C: Developer/Startup (Thử nghiệm)**
- Liên hệ trực tiếp MoMo
- Email: dev.support@momo.vn
- Yêu cầu tài khoản developer sandbox

### 1.3. Thông tin cần chuẩn bị

#### Cho Doanh nghiệp:
```
✓ Giấy phép kinh doanh (scan PDF)
✓ Mã số thuế
✓ Tên công ty (đúng trên giấy phép)
✓ Địa chỉ trụ sở chính
✓ Người đại diện pháp luật:
  - Họ tên
  - CMND/CCCD (scan 2 mặt)
  - Số điện thoại
  - Email
✓ Lĩnh vực kinh doanh
✓ Website/App URL (nếu có)
✓ Mô tả ngắn về sản phẩm/dịch vụ
```

#### Cho Developer/Startup:
```
✓ Thông tin cá nhân:
  - Họ tên
  - CMND/CCCD
  - Số điện thoại
  - Email
✓ Mô tả dự án:
  - Tên dự án
  - Mục đích (học tập/thương mại)
  - Thời gian dự kiến
  - Use case
✓ Technical info:
  - Backend tech stack
  - Expected traffic
  - Timeline
```

---

## 🔐 BƯỚC 2: NHẬN CREDENTIALS

Sau khi đăng ký được duyệt (1-3 ngày làm việc), bạn sẽ nhận được:

### 2.1. Sandbox Credentials (Test Environment)

```json
{
  "Environment": "Sandbox",
  "PartnerCode": "MOMOXXXX20250101",
  "AccessKey": "your_access_key_here",
  "SecretKey": "your_secret_key_here",
  "BaseUrl": "https://test-payment.momo.vn/v2/gateway/api/create",
  "PublicKey": "-----BEGIN PUBLIC KEY-----\n...\n-----END PUBLIC KEY-----"
}
```

### 2.2. Production Credentials (Sau khi hoàn tất testing)

```json
{
  "Environment": "Production",
  "PartnerCode": "MOMOXXXX20250101",
  "AccessKey": "prod_access_key_here",
  "SecretKey": "prod_secret_key_here",
  "BaseUrl": "https://payment.momo.vn/v2/gateway/api/create",
  "PublicKey": "-----BEGIN PUBLIC KEY-----\n...\n-----END PUBLIC KEY-----"
}
```

### 2.3. Các thông tin khác

- **Merchant ID**
- **Store ID** (nếu có nhiều cửa hàng)
- **Webhook Secret** (để verify IPN)
- **API Rate Limits**
- **Support Contact**

---

## 🔧 BƯỚC 3: CẤU HÌNH CODE

### 3.1. Update appsettings.json

Tạo 2 môi trường riêng:

```json
{
  "MoMo": {
    "Sandbox": {
      "PartnerCode": "YOUR_SANDBOX_PARTNER_CODE",
      "AccessKey": "YOUR_SANDBOX_ACCESS_KEY",
      "SecretKey": "YOUR_SANDBOX_SECRET_KEY",
      "BaseUrl": "https://test-payment.momo.vn/v2/gateway/api/create",
      "ReturnUrl": "https://localhost:7035/payment/return",
      "NotifyUrl": "https://your-domain.com/api/payments/callback/momo"
    },
    "Production": {
      "PartnerCode": "YOUR_PROD_PARTNER_CODE",
      "AccessKey": "YOUR_PROD_ACCESS_KEY",
      "SecretKey": "YOUR_PROD_SECRET_KEY",
      "BaseUrl": "https://payment.momo.vn/v2/gateway/api/create",
      "ReturnUrl": "https://yourdomain.com/payment/return",
      "NotifyUrl": "https://yourdomain.com/api/payments/callback/momo"
    },
    "Environment": "Sandbox"
  }
}
```

### 3.2. Update appsettings.Production.json

```json
{
  "MoMo": {
    "Environment": "Production"
  }
}
```

### 3.3. Update MoMoService.cs

```csharp
public class MoMoService : IMoMoService
{
    private readonly string _partnerCode;
    private readonly string _accessKey;
    private readonly string _secretKey;
    private readonly string _baseUrl;
    private readonly string _returnUrl;
    private readonly string _notifyUrl;
    private readonly string _environment;
    private readonly HttpClient _httpClient;

    public MoMoService(IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        _environment = configuration["MoMo:Environment"] ?? "Sandbox";
        var configPath = $"MoMo:{_environment}";
        
        _partnerCode = configuration[$"{configPath}:PartnerCode"] 
            ?? throw new InvalidOperationException("MoMo PartnerCode not configured");
        _accessKey = configuration[$"{configPath}:AccessKey"] 
            ?? throw new InvalidOperationException("MoMo AccessKey not configured");
        _secretKey = configuration[$"{configPath}:SecretKey"] 
            ?? throw new InvalidOperationException("MoMo SecretKey not configured");
        _baseUrl = configuration[$"{configPath}:BaseUrl"] 
            ?? throw new InvalidOperationException("MoMo BaseUrl not configured");
        _returnUrl = configuration[$"{configPath}:ReturnUrl"] 
            ?? "https://localhost:7035/payment/return";
        _notifyUrl = configuration[$"{configPath}:NotifyUrl"] 
            ?? "https://localhost:7035/api/payments/callback/momo";
        
        _httpClient = httpClientFactory.CreateClient();
        
        // Log environment (để biết đang dùng sandbox hay production)
        Console.WriteLine($"[MoMo] Initialized with {_environment} environment");
    }

    // ... rest of implementation
}
```

---

## 📱 BƯỚC 4: TESTING VỚI MOMO TEST APP

### 4.1. Tải MoMo Test App

Vẫn dùng MoMo Test App cho sandbox testing:

**Download:**
- Android: https://test-payment.momo.vn/download/
- iOS: https://test-payment.momo.vn/download/

### 4.2. Tạo Test Account

```
Số điện thoại: 0901234567 (bất kỳ)
OTP: 0000
Password: 123456
```

### 4.3. Liên kết thẻ test

```
Bank: Agribank
Card Number: 9704 0588 8888 8888
Card Holder: NGUYEN VAN A
Issue Date: 01/20
OTP: 0000
```

---

## 🧪 BƯỚC 5: TESTING FLOW

### 5.1. Create Payment Request

```http
POST https://localhost:7035/api/payments
Authorization: Bearer {{token}}
Content-Type: application/json

{
  "sessionId": 1,
  "amount": 50000,
  "paymentMethod": "momo"
}
```

**Response:**
```json
{
  "paymentId": 123,
  "paymentUrl": "https://test-payment.momo.vn/gw_payment/...",
  "qrCodeUrl": "https://test-payment.momo.vn/qr/...",
  "deeplink": "momo://app?action=payment&...",
  "status": "pending"
}
```

### 5.2. User Payment (3 options)

**Option 1: QR Code**
```
Frontend hiển thị QR Code từ qrCodeUrl
→ User mở MoMo Test App
→ Scan QR
→ Confirm payment
```

**Option 2: Deeplink (Mobile)**
```
Click deeplink
→ Auto open MoMo Test App
→ Confirm payment
```

**Option 3: Web Payment**
```
Click paymentUrl
→ Redirect to MoMo payment page
→ Login with test account
→ Confirm payment
```

### 5.3. Callback Processing

MoMo sẽ gửi IPN (Instant Payment Notification) đến NotifyUrl:

```json
{
  "partnerCode": "YOUR_PARTNER_CODE",
  "orderId": "ORDER_123",
  "requestId": "REQ_123",
  "amount": 50000,
  "orderInfo": "EV Charging Payment",
  "orderType": "momo_wallet",
  "transId": 2889912234,
  "resultCode": 0,
  "message": "Successful.",
  "payType": "qr",
  "responseTime": 1640000000000,
  "extraData": "",
  "signature": "abc123..."
}
```

**ResultCode meanings:**
- `0`: Success ✅
- `9000`: Transaction is being processed
- `10`: System error
- `11`: Transaction timeout
- `12`: User canceled
- `1001`: Payment failed
- Other: See MoMo docs

---

## 🔐 BƯỚC 6: SECURITY IMPLEMENTATION

### 6.1. Signature Generation (Request)

Theo MoMo docs, signature request được tạo như sau:

```csharp
private string GenerateRequestSignature(
    string accessKey,
    decimal amount,
    string extraData,
    string ipnUrl,
    string orderId,
    string orderInfo,
    string partnerCode,
    string redirectUrl,
    string requestId,
    string requestType)
{
    var rawHash = $"accessKey={accessKey}" +
                  $"&amount={amount}" +
                  $"&extraData={extraData}" +
                  $"&ipnUrl={ipnUrl}" +
                  $"&orderId={orderId}" +
                  $"&orderInfo={orderInfo}" +
                  $"&partnerCode={partnerCode}" +
                  $"&redirectUrl={redirectUrl}" +
                  $"&requestId={requestId}" +
                  $"&requestType={requestType}";
    
    return ComputeHmacSha256(rawHash, _secretKey);
}

private string ComputeHmacSha256(string message, string secretKey)
{
    var keyBytes = Encoding.UTF8.GetBytes(secretKey);
    var messageBytes = Encoding.UTF8.GetBytes(message);
    
    using var hmac = new HMACSHA256(keyBytes);
    var hashBytes = hmac.ComputeHash(messageBytes);
    return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
}
```

### 6.2. Signature Verification (Callback)

```csharp
private bool VerifyCallbackSignature(MoMoCallbackDto callback)
{
    var rawHash = $"accessKey={_accessKey}" +
                  $"&amount={callback.Amount}" +
                  $"&extraData={callback.ExtraData}" +
                  $"&message={callback.Message}" +
                  $"&orderId={callback.OrderId}" +
                  $"&orderInfo={callback.OrderInfo}" +
                  $"&orderType={callback.OrderType}" +
                  $"&partnerCode={callback.PartnerCode}" +
                  $"&payType={callback.PayType}" +
                  $"&requestId={callback.RequestId}" +
                  $"&responseTime={callback.ResponseTime}" +
                  $"&resultCode={callback.ResultCode}" +
                  $"&transId={callback.TransId}";
    
    var computedSignature = ComputeHmacSha256(rawHash, _secretKey);
    return computedSignature.Equals(callback.Signature, StringComparison.OrdinalIgnoreCase);
}
```

### 6.3. Whitelist MoMo IPs (Production)

Chỉ cho phép callback từ MoMo IPs:

```csharp
// Middleware hoặc filter
private static readonly string[] MoMoIPs = new[]
{
    "103.XX.XX.XX",  // MoMo sẽ cung cấp
    "103.XX.XX.XX",
    // ... add all MoMo IPs
};

public async Task InvokeAsync(HttpContext context)
{
    var remoteIp = context.Connection.RemoteIpAddress?.ToString();
    
    if (!MoMoIPs.Contains(remoteIp))
    {
        context.Response.StatusCode = 403;
        return;
    }
    
    await _next(context);
}
```

---

## 🚀 BƯỚC 7: GO TO PRODUCTION

### 7.1. Hoàn tất Testing Checklist

- [ ] Test create payment
- [ ] Test QR code payment
- [ ] Test deeplink payment
- [ ] Test web payment
- [ ] Test callback processing
- [ ] Test signature verification
- [ ] Test refund (nếu có)
- [ ] Test query transaction status
- [ ] Load testing
- [ ] Security audit

### 7.2. Yêu cầu Production Access

Liên hệ MoMo Account Manager:
- Email: merchant.support@momo.vn
- Hotline: 1900 54 54 41

Thông tin cần gửi:
```
✓ Partner Code (sandbox)
✓ Số lượng transaction test thành công
✓ Screenshots test cases
✓ Domain/URL production
✓ Expected transaction volume
✓ Go-live date
```

### 7.3. Nhận Production Credentials

MoMo sẽ gửi:
- Production PartnerCode
- Production AccessKey
- Production SecretKey
- Production BaseUrl
- IP Whitelist
- Transaction fees structure

### 7.4. Update Production Config

```json
{
  "MoMo": {
    "Production": {
      "PartnerCode": "YOUR_PROD_PARTNER_CODE",
      "AccessKey": "YOUR_PROD_ACCESS_KEY",
      "SecretKey": "YOUR_PROD_SECRET_KEY",
      "BaseUrl": "https://payment.momo.vn/v2/gateway/api/create",
      "ReturnUrl": "https://yourdomain.com/payment/return",
      "NotifyUrl": "https://yourdomain.com/api/payments/callback/momo"
    },
    "Environment": "Production"
  }
}
```

### 7.5. Deploy & Monitor

```bash
# Set environment variable
export ASPNETCORE_ENVIRONMENT=Production

# Deploy
dotnet publish -c Release
dotnet EVCharging.BE.API.dll
```

Monitor:
- Transaction success rate
- Average response time
- Error rates
- Callback delivery rate

---

## 💰 PHÍ VÀ HẠN MỨC

### Transaction Fees (Production)

| Loại giao dịch | Phí |
|---------------|-----|
| Thanh toán qua ví MoMo | 1.5% - 2.5% |
| Thanh toán qua thẻ | 2% - 3% |
| Thanh toán trả góp | 3% - 5% |

*Phí có thể thương lượng dựa trên volume*

### Limits

| Metric | Sandbox | Production |
|--------|---------|------------|
| Max transaction | 50,000,000 VND | Unlimited* |
| Daily volume | Unlimited | Theo hợp đồng |
| API Rate limit | 100 req/min | 1000 req/min |
| Concurrent requests | 10 | 100 |

*Subject to KYC và business verification*

---

## 📞 SUPPORT

### Sandbox Support
- Email: dev.support@momo.vn
- Docs: https://developers.momo.vn/
- Developer Portal: https://business.momo.vn/

### Production Support
- Hotline: 1900 54 54 41
- Email: merchant.support@momo.vn
- Account Manager: Sẽ được assign sau khi sign contract

### Emergency Contact (24/7)
- Hotline: 1900 54 54 41
- Email: support@momo.vn

---

## ✅ CHECKLIST TỔNG HỢP

### Phase 1: Registration (1-3 days)
- [ ] Đăng ký MoMo Business/Developer account
- [ ] Chuẩn bị giấy tờ (nếu cần)
- [ ] Chờ duyệt

### Phase 2: Sandbox Testing (1-2 weeks)
- [ ] Nhận sandbox credentials
- [ ] Update appsettings.json
- [ ] Tải MoMo Test App
- [ ] Implement MoMoService
- [ ] Test all payment flows
- [ ] Implement signature verification
- [ ] Test callback processing

### Phase 3: Security & Optimization (1 week)
- [ ] Audit code security
- [ ] Implement IP whitelist
- [ ] Add logging & monitoring
- [ ] Load testing
- [ ] Error handling

### Phase 4: Production (1-2 weeks)
- [ ] Yêu cầu production access
- [ ] Nhận production credentials
- [ ] Update production config
- [ ] Deploy to staging
- [ ] Final testing
- [ ] Deploy to production
- [ ] Monitor & optimize

---

## 🎯 KẾT LUẬN

### Developer Account phù hợp cho:
✅ Dự án commercial/production
✅ Cần bảo mật cao
✅ Cần support chính thức
✅ Scale lớn về sau

### Test Credentials phù hợp cho:
✅ Học tập, demo
✅ POC nhanh
✅ Dự án cá nhân
✅ Không deploy production

---

**Created:** 2025-10-29  
**Author:** AI Assistant  
**Version:** 1.0







