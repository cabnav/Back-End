using EVCharging.BE.Common.DTOs.Stations;
using EVCharging.BE.DAL.Entities;
using EVCharging.BE.Services.DTOs;

namespace EVCharging.BE.Services.Services
{
    public interface IChargingStationService
    {
        Task<IEnumerable<ChargingStation>> GetAllAsync();
        Task<ChargingStation?> GetByIdAsync(int id);
        Task<IEnumerable<StationResultDTO>> GetNearbyStationsAsync(double lat, double lon, double radiusKm);
        Task<IEnumerable<StationResultDTO>> SearchStationsAsync(StationSearchDTO filter);
        Task<object?> GetStationStatusAsync(int stationId);
    }
}
