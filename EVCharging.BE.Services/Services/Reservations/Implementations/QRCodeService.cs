using QRCoder;

namespace EVCharging.BE.Services.Services.Reservations.Implementations
{
    /// <summary>
    /// Generate QR code PNG (tạo ảnh QR dạng PNG)
    /// </summary>
    public class QRCodeService : IQRCodeService
    {
        public byte[] GenerateQRCode(string payload)
        {
            using var generator = new QRCodeGenerator();
            var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
            var png = new PngByteQRCode(data);
            return png.GetGraphic(20); // pixels per module (độ phân giải mỗi ô)
        }
    }
}
