using EVCharging.BE.Common.DTOs.Stations;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EVCharging.BE.Services.Services.Common
{
    public interface IInteractiveMapService
    {
        Task<IEnumerable<InteractiveStationDTO>> GetInteractiveStationsAsync(StationFilterDTO filter);
        Task<IEnumerable<InteractiveStationDTO>> GetNearbyStationsAsync(double latitude, double longitude, double radiusKm);
        Task<object?> GetStationStatusAsync(int stationId);
        Task<IEnumerable<InteractiveStationDTO>> GetStationsWithPricingAsync(double latitude, double longitude, bool showPeakHours);
    }
}
