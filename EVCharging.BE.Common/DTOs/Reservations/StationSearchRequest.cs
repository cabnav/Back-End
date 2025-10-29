using System;

namespace EVCharging.BE.Common.DTOs.Reservations
{
    /// <summary>
    /// Request để tìm kiếm trạm sạc phù hợp với xe và ngày đặt chỗ
    /// </summary>
    public class StationSearchRequest
    {
        /// <summary>
        /// Loại cổng sạc phù hợp với xe (CCS, CHAdeMO, Type2, etc.)
        /// Người dùng tự chọn loại cổng sạc của xe mình
        /// </summary>
        public string ConnectorType { get; set; } = null!;

        /// <summary>
        /// Ngày muốn đặt chỗ (chỉ ngày, không có giờ)
        /// </summary>
        public DateTime Date { get; set; }

        /// <summary>
        /// Vĩ độ hiện tại của người dùng (để tìm trạm gần nhất)
        /// </summary>
        public double? Latitude { get; set; }

        /// <summary>
        /// Kinh độ hiện tại của người dùng (để tìm trạm gần nhất)
        /// </summary>
        public double? Longitude { get; set; }

        /// <summary>
        /// Bán kính tìm kiếm (km) - mặc định 10km
        /// </summary>
        public double RadiusKm { get; set; } = 10;
    }
}
