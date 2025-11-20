# 📁 Hướng dẫn Copy Files vào React Project

## 🔍 Bước 1: Tìm React Project của bạn

React project thường có cấu trúc như sau:

```
SWP391/
├── Back-End/          ← Bạn đang ở đây
├── Front-End/         ← React project có thể ở đây
├── frontend/          ← Hoặc tên này
├── client/            ← Hoặc tên này
└── web-app/           ← Hoặc tên này
```

Hoặc React project có thể ở:
- Thư mục riêng biệt
- Repository Git riêng
- Trong thư mục `SWP391` cùng cấp với `Back-End`

## 📂 Bước 2: Copy Files vào React Project

Sau khi tìm thấy React project, copy các file vào vị trí sau:

### Cấu trúc thư mục React thông thường:

```
your-react-project/
├── src/
│   ├── components/          ← Copy QRScanner vào đây
│   │   ├── QRScanner.tsx
│   │   └── QRScanner.css
│   │
│   ├── pages/               ← Copy CheckInWithQRScanner vào đây
│   │   ├── CheckInWithQRScanner.tsx
│   │   └── CheckInWithQRScanner.css
│   │
│   └── ...
```

### 📋 Chi tiết copy files:

#### 1. **QRScanner Component** (BẮT BUỘC)

Copy từ Back-End:
- `QRScannerExample.tsx` 
- `QRScanner.css`

Vào React project:
```
src/components/QRScanner.tsx
src/components/QRScanner.css
```

#### 2. **CheckInWithQRScanner Component** (Tùy chọn - cho màn hình check-in)

Copy từ Back-End:
- `CheckInWithQRScanner.tsx`
- `CheckInWithQRScanner.css`

Vào React project:
```
src/pages/CheckInWithQRScanner.tsx
src/pages/CheckInWithQRScanner.css
```

**HOẶC** nếu bạn có thư mục khác:
```
src/views/CheckInWithQRScanner.tsx
src/views/CheckInWithQRScanner.css

HOẶC

src/screens/CheckInWithQRScanner.tsx
src/screens/CheckInWithQRScanner.css
```

## 🎯 Cách xác định vị trí chính xác

### Nếu React project có cấu trúc:

```
src/
├── components/     ← Components dùng chung
├── pages/         ← Các trang/màn hình
├── views/         ← Hoặc views
├── screens/       ← Hoặc screens
└── App.tsx
```

Thì:
- **QRScanner** → `src/components/` (vì là component dùng chung)
- **CheckInWithQRScanner** → `src/pages/` hoặc `src/views/` hoặc `src/screens/` (tùy theo cấu trúc của bạn)

## ✅ Checklist

- [ ] Tìm được React project
- [ ] Copy `QRScannerExample.tsx` → `src/components/QRScanner.tsx`
- [ ] Copy `QRScanner.css` → `src/components/QRScanner.css`
- [ ] Copy `CheckInWithQRScanner.tsx` → `src/pages/CheckInWithQRScanner.tsx` (hoặc views/screens)
- [ ] Copy `CheckInWithQRScanner.css` → `src/pages/CheckInWithQRScanner.css` (hoặc views/screens)
- [ ] Cài đặt thư viện: `npm install html5-qrcode`
- [ ] Sửa import path trong `CheckInWithQRScanner.tsx` nếu cần:
  ```tsx
  import { QRScanner } from "../components/QRScanner"; // Điều chỉnh path nếu cần
  ```

## 🔧 Sửa import path (nếu cần)

Sau khi copy, kiểm tra import trong `CheckInWithQRScanner.tsx`:

```tsx
// Nếu QRScanner ở src/components/
import { QRScanner } from "../components/QRScanner";

// Nếu QRScanner ở src/components/QRScanner/
import { QRScanner } from "../components/QRScanner/QRScanner";

// Nếu cùng thư mục
import { QRScanner } from "./QRScanner";
```

## 📝 Ví dụ cấu trúc hoàn chỉnh

```
your-react-project/
├── package.json
├── src/
│   ├── components/
│   │   ├── QRScanner.tsx          ← Copy từ QRScannerExample.tsx
│   │   └── QRScanner.css           ← Copy từ QRScanner.css
│   │
│   ├── pages/
│   │   ├── CheckInWithQRScanner.tsx ← Copy từ CheckInWithQRScanner.tsx
│   │   └── CheckInWithQRScanner.css ← Copy từ CheckInWithQRScanner.css
│   │
│   └── App.tsx
└── ...
```

## ❓ Nếu không tìm thấy React project

Nếu bạn chưa có React project, có thể:

1. **Tạo mới React project:**
   ```bash
   npx create-react-app frontend --template typescript
   cd frontend
   ```

2. **Hoặc cho tôi biết:**
   - React project của bạn ở đâu?
   - Cấu trúc thư mục như thế nào?
   - Tên thư mục là gì?

Tôi sẽ hướng dẫn cụ thể hơn!

