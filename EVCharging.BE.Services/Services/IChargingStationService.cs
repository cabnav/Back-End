using EVCharging.BE.DAL.Entities;
using EVCharging.BE.Services.DTOs;

namespace EVCharging.BE.Services.Services
{
    public interface IChargingStationService
    {
        Task<IEnumerable<ChargingStation>> GetAllAsync();
        Task<ChargingStation?> GetByIdAsync(int id);

        // ✅ Thêm 3 method mới
        Task<IEnumerable<ChargingStation>> GetNearbyStationsAsync(double lat, double lon, double radiusKm);
        Task<IEnumerable<ChargingStation>> SearchStationsAsync(StationSearchDTO filter);
        Task<object?> GetStationStatusAsync(int stationId);
    }
}
