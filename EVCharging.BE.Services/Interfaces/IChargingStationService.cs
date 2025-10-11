using EVCharging.BE.DAL.Entities;

namespace EVCharging.BE.Services.Interfaces
{
    public interface IChargingStationService
    {
        Task<IEnumerable<ChargingStation>> GetAllAsync();
        Task<ChargingStation?> GetByIdAsync(int id);
    }
}
