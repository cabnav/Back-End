# Hướng dẫn tích hợp QR Scanner bằng Camera cho React Web App

## 📦 Cài đặt thư viện

```bash
npm install html5-qrcode
# hoặc
yarn add html5-qrcode
```

## 🎯 Component QR Scanner

Tạo file `QRScanner.tsx` hoặc `QRScanner.jsx`:

```tsx
import { Html5Qrcode } from "html5-qrcode";
import { useEffect, useRef, useState } from "react";

interface QRScannerProps {
  onScanSuccess: (decodedText: string) => void;
  onScanError?: (error: string) => void;
  onClose?: () => void;
  fps?: number; // Frames per second (mặc định 10)
}

export const QRScanner: React.FC<QRScannerProps> = ({
  onScanSuccess,
  onScanError,
  onClose,
  fps = 10,
}) => {
  const scannerRef = useRef<Html5Qrcode | null>(null);
  const [isScanning, setIsScanning] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [cameraId, setCameraId] = useState<string | null>(null);

  // Lấy danh sách camera và chọn camera sau (back camera)
  useEffect(() => {
    const getCameras = async () => {
      try {
        const devices = await Html5Qrcode.getCameras();
        if (devices && devices.length > 0) {
          // Ưu tiên camera sau (back camera) nếu có
          const backCamera = devices.find(
            (device) => device.label.toLowerCase().includes("back") || 
                       device.label.toLowerCase().includes("rear") ||
                       device.label.toLowerCase().includes("environment")
          );
          setCameraId(backCamera?.id || devices[devices.length - 1].id);
        }
      } catch (err) {
        console.error("Error getting cameras:", err);
        setError("Không thể truy cập camera. Vui lòng kiểm tra quyền truy cập.");
      }
    };

    getCameras();
  }, []);

  // Bắt đầu quét QR
  useEffect(() => {
    if (!cameraId || isScanning) return;

    const startScanning = async () => {
      try {
        const scanner = new Html5Qrcode("qr-reader");
        scannerRef.current = scanner;

        await scanner.start(
          cameraId,
          {
            fps: fps,
            qrbox: { width: 250, height: 250 }, // Kích thước vùng quét
            aspectRatio: 1.0,
          },
          (decodedText) => {
            // ✅ Quét thành công
            onScanSuccess(decodedText);
            // Dừng scanner sau khi quét thành công
            stopScanning();
          },
          (errorMessage) => {
            // Bỏ qua lỗi "NotFoundException" (chưa tìm thấy QR code)
            if (errorMessage !== "NotFoundException") {
              console.debug("QR scan error:", errorMessage);
            }
          }
        );

        setIsScanning(true);
        setError(null);
      } catch (err: any) {
        console.error("Error starting scanner:", err);
        setError(err.message || "Không thể khởi động camera. Vui lòng thử lại.");
        setIsScanning(false);
      }
    };

    startScanning();

    // Cleanup khi component unmount
    return () => {
      stopScanning();
    };
  }, [cameraId, fps, onScanSuccess]);

  const stopScanning = async () => {
    if (scannerRef.current && isScanning) {
      try {
        await scannerRef.current.stop();
        await scannerRef.current.clear();
        scannerRef.current = null;
        setIsScanning(false);
      } catch (err) {
        console.error("Error stopping scanner:", err);
      }
    }
  };

  const handleClose = () => {
    stopScanning();
    onClose?.();
  };

  return (
    <div className="qr-scanner-container">
      <div className="qr-scanner-header">
        <h3>Quét mã QR điểm sạc</h3>
        <button onClick={handleClose} className="close-btn">
          ✕
        </button>
      </div>

      {error && (
        <div className="error-message">
          {error}
        </div>
      )}

      <div id="qr-reader" style={{ width: "100%", maxWidth: "500px" }}></div>

      {!isScanning && !error && (
        <div className="loading">Đang khởi động camera...</div>
      )}

      <div className="qr-scanner-footer">
        <p className="hint">
          Đưa mã QR của điểm sạc vào khung hình để quét tự động
        </p>
      </div>
    </div>
  );
};
```

## 🎨 CSS Styles (QRScanner.css)

```css
.qr-scanner-container {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.9);
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  z-index: 1000;
  padding: 20px;
}

.qr-scanner-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  width: 100%;
  max-width: 500px;
  margin-bottom: 20px;
  color: white;
}

.qr-scanner-header h3 {
  margin: 0;
  color: white;
}

.close-btn {
  background: rgba(255, 255, 255, 0.2);
  border: none;
  color: white;
  font-size: 24px;
  width: 40px;
  height: 40px;
  border-radius: 50%;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: background 0.3s;
}

.close-btn:hover {
  background: rgba(255, 255, 255, 0.3);
}

#qr-reader {
  background: white;
  border-radius: 8px;
  padding: 10px;
  box-shadow: 0 4px 6px rgba(0, 0, 0, 0.3);
}

.error-message {
  background: #ff4444;
  color: white;
  padding: 12px 20px;
  border-radius: 4px;
  margin-bottom: 20px;
  max-width: 500px;
  text-align: center;
}

.loading {
  color: white;
  margin-top: 20px;
  text-align: center;
}

.qr-scanner-footer {
  margin-top: 20px;
  text-align: center;
  max-width: 500px;
}

.hint {
  color: white;
  font-size: 14px;
  margin: 0;
}
```

## 🔌 Tích hợp vào Check-in Component

Ví dụ sử dụng trong component check-in:

```tsx
import { useState } from "react";
import { QRScanner } from "./QRScanner";
import axios from "axios";

export const CheckInPage = () => {
  const [showScanner, setShowScanner] = useState(false);
  const [reservationCode, setReservationCode] = useState("");
  const [pointQrCode, setPointQrCode] = useState("");
  const [initialSOC, setInitialSOC] = useState(10);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState(false);

  const handleQRScan = (decodedText: string) => {
    // ✅ Lấy mã QR từ kết quả quét
    // Format có thể là: "POINT-15" hoặc chỉ "15" hoặc full URL
    // Parse để lấy mã điểm sạc
    let qrCode = decodedText.trim();
    
    // Nếu QR code chứa URL, extract mã từ URL
    if (qrCode.includes("POINT-")) {
      qrCode = qrCode.split("POINT-")[1]?.split(/[\s\n]/)[0] || qrCode;
    }
    
    setPointQrCode(qrCode);
    setShowScanner(false);
  };

  const handleCheckIn = async () => {
    if (!reservationCode || !pointQrCode) {
      setError("Vui lòng nhập mã đặt chỗ và quét mã QR điểm sạc");
      return;
    }

    setLoading(true);
    setError(null);
    setSuccess(false);

    try {
      const token = localStorage.getItem("token"); // Hoặc cách lấy token của bạn
      
      const response = await axios.post(
        `/api/reservations/${reservationCode}/check-in`,
        {
          PointQrCode: pointQrCode,
          InitialSOC: initialSOC,
        },
        {
          headers: {
            Authorization: `Bearer ${token}`,
            "Content-Type": "application/json",
          },
        }
      );

      setSuccess(true);
      console.log("Check-in thành công:", response.data);
      
      // Redirect hoặc hiển thị thông báo thành công
      // Ví dụ: navigate("/charging-session", { state: response.data.data });
    } catch (err: any) {
      const errorMessage = err.response?.data?.message || "Có lỗi xảy ra khi check-in";
      setError(errorMessage);
      console.error("Check-in error:", err);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="check-in-page">
      <h2>Check-in đặt chỗ</h2>

      <div className="form-group">
        <label>Mã đặt chỗ (Reservation Code)</label>
        <input
          type="text"
          value={reservationCode}
          onChange={(e) => setReservationCode(e.target.value)}
          placeholder="Nhập mã đặt chỗ"
        />
      </div>

      <div className="form-group">
        <label>Mã QR điểm sạc</label>
        <div className="qr-input-group">
          <input
            type="text"
            value={pointQrCode}
            onChange={(e) => setPointQrCode(e.target.value)}
            placeholder="Quét mã QR hoặc nhập thủ công"
            readOnly={!!pointQrCode}
          />
          <button
            type="button"
            onClick={() => setShowScanner(true)}
            className="scan-btn"
          >
            📷 Quét QR
          </button>
        </div>
      </div>

      <div className="form-group">
        <label>Phần trăm pin hiện tại (Initial SOC)</label>
        <input
          type="number"
          min="0"
          max="100"
          value={initialSOC}
          onChange={(e) => setInitialSOC(parseInt(e.target.value) || 0)}
        />
      </div>

      {error && <div className="error">{error}</div>}
      {success && <div className="success">Check-in thành công!</div>}

      <button
        onClick={handleCheckIn}
        disabled={loading || !reservationCode || !pointQrCode}
        className="check-in-btn"
      >
        {loading ? "Đang xử lý..." : "Check-in"}
      </button>

      {/* QR Scanner Modal */}
      {showScanner && (
        <QRScanner
          onScanSuccess={handleQRScan}
          onScanError={(err) => console.error("Scan error:", err)}
          onClose={() => setShowScanner(false)}
        />
      )}
    </div>
  );
};
```

## 🔒 Xử lý Permissions

Thêm vào component để xử lý quyền truy cập camera:

```tsx
// Kiểm tra quyền camera trước khi mở scanner
const checkCameraPermission = async (): Promise<boolean> => {
  try {
    const stream = await navigator.mediaDevices.getUserMedia({ video: true });
    stream.getTracks().forEach(track => track.stop()); // Dừng stream ngay
    return true;
  } catch (err: any) {
    if (err.name === "NotAllowedError") {
      alert("Vui lòng cho phép truy cập camera để quét QR code");
    } else if (err.name === "NotFoundError") {
      alert("Không tìm thấy camera trên thiết bị này");
    } else {
      alert("Lỗi truy cập camera: " + err.message);
    }
    return false;
  }
};

// Sử dụng:
const handleOpenScanner = async () => {
  const hasPermission = await checkCameraPermission();
  if (hasPermission) {
    setShowScanner(true);
  }
};
```

## 📱 Responsive & Mobile Support

Thêm vào CSS để hỗ trợ mobile tốt hơn:

```css
@media (max-width: 768px) {
  .qr-scanner-container {
    padding: 10px;
  }

  #qr-reader {
    width: 100% !important;
  }

  .qr-scanner-header h3 {
    font-size: 18px;
  }
}
```

## ✅ Checklist triển khai

- [ ] Cài đặt `html5-qrcode`
- [ ] Tạo component `QRScanner`
- [ ] Thêm CSS styles
- [ ] Tích hợp vào check-in page
- [ ] Test trên desktop browser
- [ ] Test trên mobile browser
- [ ] Xử lý permissions
- [ ] Xử lý error cases
- [ ] Test với các loại QR code khác nhau

## 🐛 Troubleshooting

1. **Camera không hoạt động**: Kiểm tra HTTPS (camera chỉ hoạt động trên HTTPS hoặc localhost)
2. **Quét không chính xác**: Điều chỉnh `fps` và `qrbox` size
3. **Lỗi permissions**: Thêm thông báo rõ ràng cho người dùng
4. **Mobile không hoạt động**: Đảm bảo dùng HTTPS và test trên thiết bị thật

## 📚 Tài liệu tham khảo

- [html5-qrcode GitHub](https://github.com/mebjas/html5-qrcode)
- [MDN MediaDevices.getUserMedia()](https://developer.mozilla.org/en-US/docs/Web/API/MediaDevices/getUserMedia)

