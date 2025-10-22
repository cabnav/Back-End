using EVCharging.BE.Common.DTOs.Stations;
using EVCharging.BE.DAL.Entities;
using EVCharging.BE.Services.DTOs;

namespace EVCharging.BE.Services.Services.Charging
{
    public interface IChargingStationService
    {
        Task<IEnumerable<ChargingStation>> GetAllAsync();
        Task<ChargingStation?> GetByIdAsync(int id);
        Task<IEnumerable<StationResultDTO>> SearchStationsAsync(StationSearchDTO filter);
        Task<object?> GetStationStatusAsync(int stationId);
        
        // New interactive map methods
        Task<IEnumerable<InteractiveStationDTO>> GetInteractiveStationsAsync(StationFilterDTO filter);
        Task<object> GetRealTimeStationStatusAsync(int stationId);
    }
}
