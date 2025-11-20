/**
 * Component Check-in với QR Scanner tích hợp sẵn
 * Dùng cho màn hình "Check-in Sạc" như trong hình
 */

import { useState } from "react";
import { QRScanner } from "../components/QRScanner"; // Import QRScanner component
import "./CheckInWithQRScanner.css";

interface CheckInWithQRScannerProps {
  reservationCode?: string; // Mã đặt chỗ (có thể truyền từ props hoặc để user nhập)
  onCheckInSuccess?: (data: any) => void;
  onBack?: () => void;
}

export const CheckInWithQRScanner: React.FC<CheckInWithQRScannerProps> = ({
  reservationCode: initialReservationCode,
  onCheckInSuccess,
  onBack,
}) => {
  const [reservationCode, setReservationCode] = useState(initialReservationCode || "");
  const [pointQrCode, setPointQrCode] = useState("");
  const [showScanner, setShowScanner] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Parse QR code từ kết quả quét
  const parseQRCode = (decodedText: string): string => {
    let qrCode = decodedText.trim();
    
    // Tìm pattern POINT-{number}
    const pointMatch = qrCode.match(/POINT-(\d+)/i);
    if (pointMatch) {
      return `POINT-${pointMatch[1]}`;
    }
    
    // Nếu chỉ là số, thêm prefix POINT-
    if (/^\d+$/.test(qrCode)) {
      return `POINT-${qrCode}`;
    }
    
    return qrCode;
  };

  const handleQRScan = (decodedText: string) => {
    const parsedCode = parseQRCode(decodedText);
    setPointQrCode(parsedCode);
    setShowScanner(false);
    setError(null);
  };

  const handleOpenScanner = async () => {
    // Kiểm tra quyền camera
    try {
      const stream = await navigator.mediaDevices.getUserMedia({ video: true });
      stream.getTracks().forEach((track) => track.stop());
      setShowScanner(true);
      setError(null);
    } catch (err: any) {
      if (err.name === "NotAllowedError") {
        setError("Vui lòng cho phép truy cập camera để quét QR code");
      } else if (err.name === "NotFoundError") {
        setError("Không tìm thấy camera trên thiết bị này");
      } else {
        setError("Lỗi truy cập camera: " + err.message);
      }
    }
  };

  const handleContinue = async () => {
    if (!reservationCode.trim()) {
      setError("Vui lòng nhập mã đặt chỗ");
      return;
    }

    if (!pointQrCode.trim()) {
      setError("Vui lòng quét mã QR điểm sạc");
      return;
    }

    setLoading(true);
    setError(null);

    try {
      const token = localStorage.getItem("token") || sessionStorage.getItem("token");
      
      if (!token) {
        throw new Error("Bạn chưa đăng nhập. Vui lòng đăng nhập lại.");
      }

      // Gọi API check-in
      const response = await fetch(
        `${process.env.REACT_APP_API_URL || ""}/api/reservations/${reservationCode}/check-in`,
        {
          method: "POST",
          headers: {
            Authorization: `Bearer ${token}`,
            "Content-Type": "application/json",
          },
          body: JSON.stringify({
            PointQrCode: pointQrCode.trim(),
            InitialSOC: 10, // Có thể thêm input cho InitialSOC nếu cần
          }),
        }
      );

      if (!response.ok) {
        const errorData = await response.json();
        throw new Error(errorData.message || "Có lỗi xảy ra khi check-in");
      }

      const data = await response.json();
      onCheckInSuccess?.(data);
      
      // Có thể navigate đến trang theo dõi session
      // navigate("/charging-session", { state: data.data });

    } catch (err: any) {
      setError(err.message || "Có lỗi xảy ra khi check-in");
    } finally {
      setLoading(false);
    }
  };

  const canContinue = reservationCode.trim() && pointQrCode.trim() && !loading;

  return (
    <div className="check-in-screen">
      {/* Header */}
      <div className="check-in-header">
        <button onClick={onBack} className="back-button">
          ←
        </button>
        <h1 className="check-in-title">Check-in Sạc</h1>
      </div>

      {/* Reservation Code Section */}
      <div className="reservation-code-box">
        <div className="reservation-code-label">Mã đặt chỗ</div>
        <input
          type="text"
          value={reservationCode}
          onChange={(e) => setReservationCode(e.target.value.toUpperCase())}
          placeholder="Nhập mã đặt chỗ"
          className="reservation-code-input"
          disabled={loading}
        />
      </div>

      {/* QR Scan Section */}
      <div className="qr-scan-section">
        <div className="qr-icon">📷</div>
        <h2 className="qr-scan-title">Scan mã QR trạm sạc</h2>
        <p className="qr-scan-hint">
          Hãy quét mã QR trên trạm sạc hoặc nhập mã thủ công
        </p>
        
        <div className="qr-input-container">
          <div className="qr-input-wrapper">
            <span className="qr-input-icon">🔲</span>
            <input
              type="text"
              value={pointQrCode}
              onChange={(e) => setPointQrCode(e.target.value)}
              placeholder="Scan hoặc nhập mã QR..."
              className="qr-input-field"
              disabled={loading || showScanner}
            />
            {/* ✅ Button quét QR ngay trong input field */}
            <button
              onClick={handleOpenScanner}
              className="scan-qr-icon-btn"
              disabled={loading || showScanner}
              type="button"
              title="Quét QR bằng Camera"
            >
              📷
            </button>
            {pointQrCode && (
              <button
                onClick={() => setPointQrCode("")}
                className="clear-qr-btn"
                disabled={loading || showScanner}
                type="button"
              >
                ✕
              </button>
            )}
          </div>
          
          {/* ✅ Button mở camera scanner (lớn, nổi bật) */}
          <button
            onClick={handleOpenScanner}
            className="scan-qr-button"
            disabled={loading || showScanner}
            type="button"
          >
            📷 Quét QR bằng Camera
          </button>
        </div>
      </div>

      {/* Error Message */}
      {error && (
        <div className="error-message">
          {error}
        </div>
      )}

      {/* Navigation Buttons */}
      <div className="check-in-footer">
        <button
          onClick={onBack}
          className="back-footer-button"
          disabled={loading}
        >
          ← Quay lại
        </button>
        <button
          onClick={handleContinue}
          disabled={!canContinue}
          className={`continue-button ${canContinue ? "active" : ""}`}
        >
          {loading ? "Đang xử lý..." : "Tiếp tục →"}
        </button>
      </div>

      {/* QR Scanner Modal */}
      {showScanner && (
        <QRScanner
          onScanSuccess={handleQRScan}
          onScanError={(err: string) => {
            // Không hiển thị lỗi cho user vì đây là lỗi thường xuyên khi chưa quét được
            console.debug("Scan error:", err);
          }}
          onClose={() => setShowScanner(false)}
        />
      )}
    </div>
  );
};

