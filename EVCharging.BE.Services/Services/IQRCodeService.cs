namespace EVCharging.BE.Services.Services
{
    public interface IQRCodeService
    {
        /// <summary>
        /// Sinh mã QR dưới dạng hình ảnh PNG (định dạng Portable Network Graphics)
        /// </summary>
        /// <param name="payload">Dữ liệu cần mã hoá vào QR (ví dụ: mã đặt chỗ, mã điểm sạc)</param>
        /// <returns>Mảng byte của ảnh PNG</returns>
        byte[] GenerateQRCode(string payload);
    }
}
