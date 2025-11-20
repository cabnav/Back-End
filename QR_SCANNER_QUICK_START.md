# 🚀 Quick Start - QR Scanner cho React Web App

## Bước 1: Cài đặt thư viện

```bash
npm install html5-qrcode
# hoặc
yarn add html5-qrcode
```

## Bước 2: Copy files vào project

Copy các file sau vào thư mục React project của bạn:

1. **`QRScannerExample.tsx`** → `src/components/QRScanner.tsx`
2. **`QRScanner.css`** → `src/components/QRScanner.css`
3. **`CheckInPageExample.tsx`** → `src/pages/CheckInPage.tsx` (optional - chỉ để tham khảo)
4. **`CheckInPage.css`** → `src/pages/CheckInPage.css` (optional)

## Bước 3: Sử dụng component

```tsx
import { useState } from "react";
import { QRScanner } from "./components/QRScanner";

function App() {
  const [showScanner, setShowScanner] = useState(false);
  const [qrCode, setQrCode] = useState("");

  return (
    <div>
      <button onClick={() => setShowScanner(true)}>
        Quét QR Code
      </button>
      
      {showScanner && (
        <QRScanner
          onScanSuccess={(text) => {
            setQrCode(text);
            setShowScanner(false);
          }}
          onClose={() => setShowScanner(false)}
        />
      )}
      
      {qrCode && <p>QR Code: {qrCode}</p>}
    </div>
  );
}
```

## Bước 4: Tích hợp với API Check-in

Xem file `CheckInPageExample.tsx` để biết cách tích hợp với API:

```tsx
// Gọi API sau khi quét QR thành công
const response = await axios.post(
  `/api/reservations/${reservationCode}/check-in`,
  {
    PointQrCode: pointQrCode, // Mã QR đã quét
    InitialSOC: initialSOC,
  },
  {
    headers: {
      Authorization: `Bearer ${token}`,
    },
  }
);
```

## ⚠️ Lưu ý quan trọng

1. **HTTPS Required**: Camera chỉ hoạt động trên HTTPS hoặc `localhost`
2. **Permissions**: Browser sẽ yêu cầu quyền truy cập camera
3. **Mobile**: Test trên thiết bị thật để đảm bảo hoạt động tốt

## 📚 Tài liệu đầy đủ

Xem file `QR_SCANNER_IMPLEMENTATION.md` để biết chi tiết và troubleshooting.

## ✅ Checklist

- [ ] Cài đặt `html5-qrcode`
- [ ] Copy component vào project
- [ ] Test trên desktop (localhost)
- [ ] Test trên mobile (HTTPS)
- [ ] Tích hợp với API check-in
- [ ] Xử lý error cases

