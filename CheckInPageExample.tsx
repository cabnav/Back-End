/**
 * Example Check-in Page với QR Scanner
 * 
 * Component này tích hợp QR Scanner vào flow check-in reservation
 */

import { useState } from "react";
import { QRScanner } from "./QRScannerExample";
import axios from "axios";
import "./CheckInPage.css";

interface CheckInResponse {
  message: string;
  data: {
    SessionId: number;
    Status: string;
    StartTime: string;
    // ... other session data
  };
}

export const CheckInPage: React.FC = () => {
  const [showScanner, setShowScanner] = useState(false);
  const [reservationCode, setReservationCode] = useState("");
  const [pointQrCode, setPointQrCode] = useState("");
  const [initialSOC, setInitialSOC] = useState(10);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState(false);
  const [sessionData, setSessionData] = useState<any>(null);

  // Parse QR code từ kết quả quét
  const parseQRCode = (decodedText: string): string => {
    let qrCode = decodedText.trim();

    // Nếu QR code chứa URL hoặc format phức tạp, extract mã điểm sạc
    // Format có thể là:
    // - "POINT-15"
    // - "https://example.com/point/POINT-15"
    // - Chỉ số "15"
    // - Hoặc format khác từ backend

    // Tìm pattern POINT-{number}
    const pointMatch = qrCode.match(/POINT-(\d+)/i);
    if (pointMatch) {
      return `POINT-${pointMatch[1]}`;
    }

    // Nếu chỉ là số, thêm prefix POINT-
    if (/^\d+$/.test(qrCode)) {
      return `POINT-${qrCode}`;
    }

    // Nếu có URL, extract từ path
    try {
      const url = new URL(qrCode);
      const pathParts = url.pathname.split("/");
      const pointPart = pathParts.find((part) => part.includes("POINT") || /^\d+$/.test(part));
      if (pointPart) {
        return pointPart.includes("POINT") ? pointPart : `POINT-${pointPart}`;
      }
    } catch {
      // Không phải URL, giữ nguyên
    }

    return qrCode;
  };

  const handleQRScan = (decodedText: string) => {
    const parsedCode = parseQRCode(decodedText);
    setPointQrCode(parsedCode);
    setShowScanner(false);
    setError(null);
  };

  const handleCheckIn = async () => {
    // Validation
    if (!reservationCode.trim()) {
      setError("Vui lòng nhập mã đặt chỗ");
      return;
    }

    if (!pointQrCode.trim()) {
      setError("Vui lòng quét mã QR điểm sạc hoặc nhập thủ công");
      return;
    }

    if (initialSOC < 0 || initialSOC > 100) {
      setError("Phần trăm pin phải từ 0 đến 100");
      return;
    }

    setLoading(true);
    setError(null);
    setSuccess(false);
    setSessionData(null);

    try {
      // Lấy token từ localStorage hoặc context
      const token = localStorage.getItem("token") || sessionStorage.getItem("token");
      
      if (!token) {
        throw new Error("Bạn chưa đăng nhập. Vui lòng đăng nhập lại.");
      }

      // Gọi API check-in
      const response = await axios.post<CheckInResponse>(
        `${process.env.REACT_APP_API_URL || ""}/api/reservations/${reservationCode}/check-in`,
        {
          PointQrCode: pointQrCode.trim(),
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
      setSessionData(response.data.data);
      
      console.log("Check-in thành công:", response.data);

      // Có thể redirect đến trang theo dõi session
      // navigate("/charging-session", { state: response.data.data });
      
      // Hoặc hiển thị thông báo và reset form sau 3 giây
      setTimeout(() => {
        // Reset form
        setReservationCode("");
        setPointQrCode("");
        setInitialSOC(10);
        setSuccess(false);
      }, 3000);

    } catch (err: any) {
      console.error("Check-in error:", err);
      
      let errorMessage = "Có lỗi xảy ra khi check-in";
      
      if (err.response) {
        // Server trả về lỗi
        errorMessage = err.response.data?.message || errorMessage;
      } else if (err.request) {
        // Request được gửi nhưng không có response
        errorMessage = "Không thể kết nối đến server. Vui lòng kiểm tra kết nối mạng.";
      } else if (err.message) {
        // Lỗi khác
        errorMessage = err.message;
      }

      setError(errorMessage);
    } finally {
      setLoading(false);
    }
  };

  const handleOpenScanner = async () => {
    // Kiểm tra quyền camera trước
    try {
      const stream = await navigator.mediaDevices.getUserMedia({ video: true });
      stream.getTracks().forEach((track) => track.stop()); // Dừng stream ngay
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

  return (
    <div className="check-in-page">
      <div className="check-in-container">
        <h2>Check-in đặt chỗ</h2>
        <p className="subtitle">Nhập mã đặt chỗ và quét mã QR điểm sạc để bắt đầu sạc</p>

        <div className="form-group">
          <label htmlFor="reservation-code">
            Mã đặt chỗ (Reservation Code) <span className="required">*</span>
          </label>
          <input
            id="reservation-code"
            type="text"
            value={reservationCode}
            onChange={(e) => setReservationCode(e.target.value.toUpperCase())}
            placeholder="Nhập mã đặt chỗ (ví dụ: RES-123456)"
            disabled={loading}
          />
        </div>

        <div className="form-group">
          <label htmlFor="point-qr-code">
            Mã QR điểm sạc <span className="required">*</span>
          </label>
          <div className="qr-input-group">
            <input
              id="point-qr-code"
              type="text"
              value={pointQrCode}
              onChange={(e) => setPointQrCode(e.target.value)}
              placeholder="Quét mã QR hoặc nhập thủ công (ví dụ: POINT-15)"
              disabled={loading || showScanner}
            />
            <button
              type="button"
              onClick={handleOpenScanner}
              className="scan-btn"
              disabled={loading || showScanner}
            >
              📷 Quét QR
            </button>
          </div>
          {pointQrCode && (
            <button
              type="button"
              onClick={() => setPointQrCode("")}
              className="clear-btn"
            >
              ✕ Xóa
            </button>
          )}
        </div>

        <div className="form-group">
          <label htmlFor="initial-soc">
            Phần trăm pin hiện tại (Initial SOC) <span className="required">*</span>
          </label>
          <input
            id="initial-soc"
            type="number"
            min="0"
            max="100"
            value={initialSOC}
            onChange={(e) => setInitialSOC(parseInt(e.target.value) || 0)}
            disabled={loading}
          />
          <div className="soc-slider-container">
            <input
              type="range"
              min="0"
              max="100"
              value={initialSOC}
              onChange={(e) => setInitialSOC(parseInt(e.target.value))}
              disabled={loading}
              className="soc-slider"
            />
            <div className="soc-labels">
              <span>0%</span>
              <span>50%</span>
              <span>100%</span>
            </div>
          </div>
        </div>

        {error && (
          <div className="error-message">
            <strong>Lỗi:</strong> {error}
          </div>
        )}

        {success && (
          <div className="success-message">
            <strong>Thành công!</strong> Check-in hoàn tất. Phiên sạc đã được bắt đầu.
            {sessionData && (
              <div className="session-info">
                <p>Session ID: {sessionData.SessionId}</p>
                <p>Trạng thái: {sessionData.Status}</p>
              </div>
            )}
          </div>
        )}

        <button
          onClick={handleCheckIn}
          disabled={loading || !reservationCode || !pointQrCode || showScanner}
          className="check-in-btn"
        >
          {loading ? (
            <>
              <span className="spinner"></span>
              Đang xử lý...
            </>
          ) : (
            "Check-in"
          )}
        </button>
      </div>

      {/* QR Scanner Modal */}
      {showScanner && (
        <QRScanner
          onScanSuccess={handleQRScan}
          onScanError={(err) => {
            console.error("Scan error:", err);
            // Không hiển thị lỗi cho user vì đây là lỗi thường xuyên khi chưa quét được
          }}
          onClose={() => setShowScanner(false)}
        />
      )}
    </div>
  );
};

