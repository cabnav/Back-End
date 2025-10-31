using EVCharging.BE.Common.DTOs.Charging;

namespace EVCharging.BE.Common.DTOs.Staff
{
    /// <summary>
    /// Response khi khởi động phiên sạc walk-in thành công
    /// </summary>
    public class WalkInSessionResponse
    {
        /// <summary>
        /// Thông tin phiên sạc
        /// </summary>
        public ChargingSessionResponse Session { get; set; } = new();

        /// <summary>
        /// QR Code để khách có thể theo dõi (optional)
        /// </summary>
        public string? QrCode { get; set; }

        /// <summary>
        /// Chi phí ước tính
        /// </summary>
        public decimal EstimatedCost { get; set; }

        /// <summary>
        /// Thời gian ước tính hoàn thành
        /// </summary>
        public DateTime EstimatedCompletionTime { get; set; }

        /// <summary>
        /// Phương thức thanh toán
        /// </summary>
        public string PaymentMethod { get; set; } = string.Empty;

        /// <summary>
        /// Hướng dẫn thanh toán cho khách
        /// </summary>
        public string PaymentInstructions { get; set; } = string.Empty;

        /// <summary>
        /// Thông tin khách hàng
        /// </summary>
        public WalkInCustomerInfo CustomerInfo { get; set; } = new();
    }

    public class WalkInCustomerInfo
    {
        public string CustomerName { get; set; } = string.Empty;
        public string? CustomerPhone { get; set; }
        public string VehiclePlate { get; set; } = string.Empty;
        public string? VehicleModel { get; set; }
    }
}

