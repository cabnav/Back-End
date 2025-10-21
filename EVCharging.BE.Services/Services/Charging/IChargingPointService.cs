using EVCharging.BE.Services.DTOs;
using EVCharging.BE.Common.DTOs.Stations;


namespace EVCharging.BE.Services.Services.Charging
{
    public interface IChargingPointService
    {
        Task<IEnumerable<ChargingPointDTO>> GetAllAsync();
        Task<ChargingPointDTO?> GetByIdAsync(int id);
        Task<IEnumerable<ChargingPointDTO>> GetAvailableAsync();
        Task<IEnumerable<ChargingPointDTO>> GetByStationAsync(int stationId);
        Task<ChargingPointDTO?> UpdateStatusAsync(int id, string newStatus);
    }
}
