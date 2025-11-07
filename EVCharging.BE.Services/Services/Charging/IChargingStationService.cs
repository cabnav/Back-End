using EVCharging.BE.Common.DTOs.Stations;
using EVCharging.BE.Common.DTOs.Stations.EVCharging.BE.Common.DTOs.Stations;

namespace EVCharging.BE.Services.Services.Charging
{
    public interface IChargingStationService
    {
        // CRUD
        Task<IEnumerable<StationDTO>> GetAllAsync();
        Task<StationDTO?> GetByIdAsync(int id);
        Task<StationDTO> CreateAsync(StationCreateRequest req);
        Task<StationDTO?> UpdateAsync(int id, StationUpdateRequest req);
        Task<bool> DeleteAsync(int id);

        // Search / Filter / Realtime
        Task<IEnumerable<InteractiveStationDTO>> GetInteractiveStationsAsync(StationFilterDTO filter);
        Task<IEnumerable<InteractiveStationDTO>> SearchStationsAsync(StationFilterDTO filter);
        Task<object?> GetRealTimeStationStatusAsync(int stationId);

    }
}
