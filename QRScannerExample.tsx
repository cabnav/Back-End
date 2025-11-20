/**
 * Component QR Scanner sử dụng html5-qrcode
 * 
 * Cài đặt: npm install html5-qrcode
 * 
 * Sử dụng:
 * ```tsx
 * <QRScanner
 *   onScanSuccess={(text) => console.log("QR Code:", text)}
 *   onClose={() => setShowScanner(false)}
 * />
 * ```
 */

import { Html5Qrcode } from "html5-qrcode";
import { useEffect, useRef, useState } from "react";
import "./QRScanner.css"; // Import CSS styles

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
  const [availableCameras, setAvailableCameras] = useState<MediaDeviceInfo[]>([]);

  // Lấy danh sách camera và chọn camera sau (back camera)
  useEffect(() => {
    const getCameras = async () => {
      try {
        const devices = await Html5Qrcode.getCameras();
        if (devices && devices.length > 0) {
          setAvailableCameras(devices);
          // Ưu tiên camera sau (back camera) nếu có
          const backCamera = devices.find(
            (device) =>
              device.label.toLowerCase().includes("back") ||
              device.label.toLowerCase().includes("rear") ||
              device.label.toLowerCase().includes("environment")
          );
          setCameraId(backCamera?.id || devices[devices.length - 1].id);
        } else {
          setError("Không tìm thấy camera trên thiết bị này.");
        }
      } catch (err: any) {
        console.error("Error getting cameras:", err);
        if (err.name === "NotAllowedError") {
          setError("Vui lòng cho phép truy cập camera để quét QR code.");
        } else if (err.name === "NotFoundError") {
          setError("Không tìm thấy camera trên thiết bị này.");
        } else {
          setError("Không thể truy cập camera. Vui lòng kiểm tra quyền truy cập.");
        }
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
            videoConstraints: {
              facingMode: "environment", // Ưu tiên camera sau
            },
          },
          (decodedText) => {
            // ✅ Quét thành công
            console.log("QR Code scanned:", decodedText);
            onScanSuccess(decodedText);
            // Dừng scanner sau khi quét thành công
            stopScanning();
          },
          (errorMessage) => {
            // Bỏ qua lỗi "NotFoundException" (chưa tìm thấy QR code)
            if (errorMessage !== "NotFoundException") {
              console.debug("QR scan error:", errorMessage);
              onScanError?.(errorMessage);
            }
          }
        );

        setIsScanning(true);
        setError(null);
      } catch (err: any) {
        console.error("Error starting scanner:", err);
        if (err.name === "NotAllowedError") {
          setError("Vui lòng cho phép truy cập camera để quét QR code.");
        } else if (err.name === "NotFoundError") {
          setError("Không tìm thấy camera trên thiết bị này.");
        } else {
          setError(err.message || "Không thể khởi động camera. Vui lòng thử lại.");
        }
        setIsScanning(false);
      }
    };

    startScanning();

    // Cleanup khi component unmount
    return () => {
      stopScanning();
    };
  }, [cameraId, fps, onScanSuccess, onScanError]);

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

  const switchCamera = async (newCameraId: string) => {
    await stopScanning();
    setCameraId(newCameraId);
  };

  return (
    <div className="qr-scanner-container">
      <div className="qr-scanner-header">
        <h3>Quét mã QR điểm sạc</h3>
        <button onClick={handleClose} className="close-btn" aria-label="Đóng">
          ✕
        </button>
      </div>

      {error && (
        <div className="error-message">
          {error}
          <button onClick={() => window.location.reload()} className="retry-btn">
            Thử lại
          </button>
        </div>
      )}

      {availableCameras.length > 1 && (
        <div className="camera-switcher">
          <label>Chọn camera: </label>
          <select
            value={cameraId || ""}
            onChange={(e) => switchCamera(e.target.value)}
            disabled={!isScanning}
          >
            {availableCameras.map((camera) => (
              <option key={camera.id} value={camera.id}>
                {camera.label || `Camera ${camera.id.substring(0, 8)}`}
              </option>
            ))}
          </select>
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
        <button onClick={handleClose} className="cancel-btn">
          Hủy
        </button>
      </div>
    </div>
  );
};

