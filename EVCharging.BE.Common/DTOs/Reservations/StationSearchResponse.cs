using EVCharging.BE.Common.DTOs.Stations;

namespace EVCharging.BE.Common.DTOs.Reservations
{
    /// <summary>
    /// Response trả về danh sách trạm sạc phù hợp
    /// </summary>
    public class StationSearchResponse
    {
        /// <summary>
        /// Thông tin trạm sạc
        /// </summary>
        public StationDTO Station { get; set; } = null!;

        /// <summary>
        /// Số điểm sạc phù hợp với xe tại trạm này
        /// </summary>
        public int CompatiblePointsCount { get; set; }

        /// <summary>
        /// Khoảng cách từ vị trí người dùng đến trạm (km)
        /// </summary>
        public double DistanceKm { get; set; }

        /// <summary>
        /// Danh sách điểm sạc phù hợp
        /// </summary>
        public List<CompatibleChargingPointDTO> CompatiblePoints { get; set; } = new();
    }
}
