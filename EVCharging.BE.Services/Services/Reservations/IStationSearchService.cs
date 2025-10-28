using EVCharging.BE.Common.DTOs.Reservations;
using EVCharging.BE.DAL;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EVCharging.BE.Services.Services.Reservations
{
    /// <summary>
    /// Service để tìm kiếm trạm sạc phù hợp với xe và khung giờ
    /// </summary>
    public interface IStationSearchService
    {
        /// <summary>
        /// Tìm kiếm trạm sạc phù hợp với xe và ngày đặt chỗ
        /// </summary>
        Task<List<StationSearchResponse>> SearchCompatibleStationsAsync(StationSearchRequest request);

        /// <summary>
        /// Lấy danh sách điểm sạc phù hợp tại một trạm cụ thể
        /// </summary>
        Task<List<CompatibleChargingPointDTO>> GetCompatiblePointsAsync(int stationId, string connectorType);

        /// <summary>
        /// Lấy danh sách khung giờ có sẵn cho một điểm sạc trong ngày cụ thể
        /// </summary>
        Task<List<TimeSlotDTO>> GetAvailableTimeSlotsAsync(int pointId, DateTime date);
    }
}
