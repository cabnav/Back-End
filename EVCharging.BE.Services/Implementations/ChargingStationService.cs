using EVCharging.BE.DAL;
using EVCharging.BE.DAL.Entities;
using EVCharging.BE.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EVCharging.BE.Services.Implementations
{
    public class ChargingStationService : IChargingStationService
    {
        private readonly EvchargingManagementContext _db;

        public ChargingStationService(EvchargingManagementContext db)
        {
            _db = db;
        }

        public async Task<IEnumerable<ChargingStation>> GetAllAsync()
            => await _db.ChargingStations.ToListAsync();

        public async Task<ChargingStation?> GetByIdAsync(int id)
            => await _db.ChargingStations.FindAsync(id);
    }
}
