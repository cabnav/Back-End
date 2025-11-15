using CP = EVCharging.BE.Common.DTOs.Stations;

namespace EVCharging.BE.Services.Services.Charging
{
    public interface IChargingPointService
    {
        Task<IEnumerable<CP.ChargingPointDTO>> GetAllAsync();
        Task<CP.ChargingPointDTO?> GetByIdAsync(int id);
        Task<IEnumerable<CP.ChargingPointDTO>> GetAvailableAsync();
        Task<IEnumerable<CP.ChargingPointDTO>> GetByStationAsync(int stationId);

        Task<CP.ChargingPointDTO?> UpdateStatusAsync(int id, string newStatus);
        Task<CP.ChargingPointDTO> CreateAsync(CP.ChargingPointCreateRequest req);
        Task<CP.ChargingPointDTO?> UpdatePriceAsync(int id, decimal newPricePerKwh);
        Task<bool> UpdateAsync(int id, CP.ChargingPointUpdateRequest req);
        Task<bool> DeleteAsync(int id);
    }
}
