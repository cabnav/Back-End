using EVCharging.BE.Common.DTOs.Stations;
using EVCharging.BE.Services.Services.Charging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EVCharging.BE.Services.Services.Common.Implementations
{
    public class InteractiveMapService : IInteractiveMapService
    {
        private readonly IChargingStationService _chargingStationService;

        public InteractiveMapService(IChargingStationService chargingStationService)
        {
            _chargingStationService = chargingStationService;
        }

        public async Task<IEnumerable<InteractiveStationDTO>> GetInteractiveStationsAsync(StationFilterDTO filter)
        {
            return await _chargingStationService.GetInteractiveStationsAsync(filter);
        }

        public async Task<IEnumerable<InteractiveStationDTO>> GetNearbyStationsAsync(double latitude, double longitude, double radiusKm)
        {
            var filter = new StationFilterDTO
            {
                Latitude = latitude,
                Longitude = longitude,
                MaxDistanceKm = radiusKm
            };
            return await _chargingStationService.GetInteractiveStationsAsync(filter);
        }

        public async Task<object?> GetStationStatusAsync(int stationId)
        {
            return await _chargingStationService.GetRealTimeStationStatusAsync(stationId);
        }

        public async Task<IEnumerable<InteractiveStationDTO>> GetStationsWithPricingAsync(double latitude, double longitude, bool showPeakHours)
        {
            var filter = new StationFilterDTO
            {
                Latitude = latitude,
                Longitude = longitude,
                MaxDistanceKm = 50 // mặc định 50km
            };

            var stations = await _chargingStationService.GetInteractiveStationsAsync(filter);

            if (showPeakHours)
                stations = stations.Where(s => s.Pricing?.PeakHourPrice > 0);

            return stations;
        }
    }
}
