# 🚀 MoMo Integration - Quick Start

## 🎯 LỰA CHỌN CỦA BẠN

### Option 1: Test Credentials (5 phút setup) ⚡
**Dùng khi:** Demo, học tập, POC nhanh

```json
{
  "MoMo": {
    "PartnerCode": "MOMOBKUN20180529",
    "AccessKey": "klm05TvNBzhg7h7j",
    "SecretKey": "at67qH6mk8w5Y1nAyMoYKMWACiEi2bsa",
    "BaseUrl": "https://test-payment.momo.vn/v2/gateway/api/create",
    "ReturnUrl": "https://localhost:7035/payment/return",
    "NotifyUrl": "https://webhook.site/your-unique-id"
  }
}
```

✅ **Ưu điểm:**
- Không cần đăng ký
- Setup ngay lập tức
- Miễn phí

❌ **Nhược điểm:**
- Credentials public (không bảo mật)
- Không thể lên production
- Không có support

---

### Option 2: Developer Account (1-3 ngày setup) 🏢
**Dùng khi:** Dự án thật, sẽ deploy production

**Bước 1:** Đăng ký
```
🔗 https://business.momo.vn/
📧 dev.support@momo.vn
📞 1900 54 54 41
```

**Bước 2:** Nhận credentials riêng
```json
{
  "MoMo": {
    "Sandbox": {
      "PartnerCode": "YOUR_CODE_HERE",
      "AccessKey": "YOUR_KEY_HERE",
      "SecretKey": "YOUR_SECRET_HERE"
    }
  }
}
```

✅ **Ưu điểm:**
- Credentials riêng, bảo mật
- Có thể lên production
- Có support team
- Không giới hạn

❌ **Nhược điểm:**
- Cần đăng ký & chờ duyệt (1-3 ngày)
- Cần giấy tờ (nếu doanh nghiệp)

---

## 📦 SETUP NGAY (5 PHÚT)

### Step 1: Update appsettings.json

```json
{
  "MoMo": {
    "PartnerCode": "MOMOBKUN20180529",
    "AccessKey": "klm05TvNBzhg7h7j",
    "SecretKey": "at67qH6mk8w5Y1nAyMoYKMWACiEi2bsa",
    "BaseUrl": "https://test-payment.momo.vn/v2/gateway/api/create",
    "ReturnUrl": "https://localhost:7035/payment/return",
    "NotifyUrl": "https://localhost:7035/api/payments/callback/momo"
  }
}
```

### Step 2: Code đã sẵn sàng!

File `MoMoService.cs` đã được implement, bạn chỉ cần:

```csharp
// PaymentsController.cs - đã có sẵn!
[HttpPost]
public async Task<IActionResult> CreatePayment([FromBody] PaymentCreateRequest request)
{
    if (request.PaymentMethod == "momo")
    {
        var momoResult = await _momoService.CreatePaymentRequestAsync(request);
        // ... xử lý kết quả
    }
}
```

### Step 3: Tải MoMo Test App

**Android/iOS:**
```
📱 https://test-payment.momo.vn/download/
```

**Đăng ký test account:**
```
📞 Số điện thoại: 0901234567 (bất kỳ)
🔐 OTP: 0000
🔑 Password: 123456
```

**Nạp tiền test:**
```
🏦 Bank: Agribank
💳 Card: 9704 0588 8888 8888
👤 Name: NGUYEN VAN A
📅 Issue: 01/20
🔐 OTP: 0000
```

### Step 4: Test ngay!

```http
### Tạo payment
POST https://localhost:7035/api/payments
Authorization: Bearer {{token}}
Content-Type: application/json

{
  "sessionId": 1,
  "amount": 50000,
  "paymentMethod": "momo"
}

### Response sẽ có paymentUrl
{
  "paymentUrl": "https://test-payment.momo.vn/...",
  "qrCodeUrl": "...",
  "deeplink": "momo://..."
}
```

---

## 📊 SO SÁNH CHI TIẾT

| Feature | Test Credentials | Developer Account |
|---------|------------------|-------------------|
| **Setup time** | 5 phút | 1-3 ngày |
| **Đăng ký** | ❌ Không | ✅ Có (online) |
| **Credentials** | Public | Riêng tư |
| **Bảo mật** | ⚠️ Thấp | ✅ Cao |
| **Production** | ❌ Không | ✅ Có |
| **Support** | ❌ Không | ✅ Email/Hotline |
| **Transaction limit** | Không giới hạn (test) | Theo hợp đồng |
| **Phí** | Miễn phí (test) | Miễn phí test, ~2% prod |
| **IP Whitelist** | ❌ Không | ✅ Có |
| **Custom config** | ❌ Không | ✅ Có |
| **Refund API** | ⚠️ Limited | ✅ Full |
| **Transaction query** | ⚠️ Limited | ✅ Full |
| **Giấy tờ cần** | ❌ Không | ✅ Có (doanh nghiệp) |

---

## 🎯 KHUYẾN NGHỊ

### Cho Development/Testing:
```
👉 Dùng Test Credentials
✅ Nhanh, đơn giản
✅ Không cần đăng ký
✅ Test full flow
```

### Cho Staging:
```
👉 Đăng ký Developer Account (Sandbox)
✅ Credentials riêng
✅ Test như production
✅ Có support
```

### Cho Production:
```
👉 Developer Account (Production)
✅ Bắt buộc
✅ Bảo mật cao
✅ Support 24/7
```

---

## 📞 HỖ TRỢ

### Test Credentials Issues:
- ❓ Docs: https://developers.momo.vn/
- 📧 Email: dev.support@momo.vn

### Developer Account Registration:
- 🔗 Portal: https://business.momo.vn/
- 📧 Email: dev.support@momo.vn
- 📞 Hotline: 1900 54 54 41

### Production Support:
- 📞 24/7 Hotline: 1900 54 54 41
- 📧 merchant.support@momo.vn

---

## 📚 NEXT STEPS

1. ✅ **Đọc guide này** ← BẠN ĐANG Ở ĐÂY
2. 📖 **Đọc chi tiết:**
   - `MOMO_DEVELOPER_ACCOUNT_GUIDE.md` - Full guide cho Developer Account
3. 🔧 **Setup:**
   - Copy `appsettings.MoMo.Example.json` → `appsettings.json`
   - Điền credentials
4. 🧪 **Test:**
   - Tải MoMo Test App
   - Tạo payment request
   - Scan QR/Click deeplink
   - Verify callback
5. 🚀 **Deploy:**
   - Đăng ký Developer Account (nếu chưa)
   - Test trên staging
   - Request production access
   - Go live!

---

**Happy Coding! 🚀**







