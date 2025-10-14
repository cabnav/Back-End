using EVCharging.BE.DAL;
using EVCharging.BE.DAL.Entities;
using EVCharging.BE.Services.Services;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EVCharging.BE.Services.Services.Implementations
{
    public class DriverProfileService : IDriverProfileService
    {
        private readonly EvchargingManagementContext _context;

        public DriverProfileService(EvchargingManagementContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<DriverProfile>> GetAllAsync()
        {
            return await _context.DriverProfiles
                .Include(d => d.User)
                .Include(d => d.Corporate)
                .ToListAsync();
        }

        public async Task<DriverProfile?> GetByIdAsync(int id)
        {
            return await _context.DriverProfiles
                .Include(d => d.User)
                .Include(d => d.Corporate)
                .FirstOrDefaultAsync(d => d.DriverId == id);
        }

        public async Task<DriverProfile> CreateAsync(DriverProfile driverProfile)
        {
            _context.DriverProfiles.Add(driverProfile);
            await _context.SaveChangesAsync();
            return driverProfile;
        }

        // ✅ Đã fix theo model của bạn (không còn VehicleType)
        public async Task<bool> UpdateAsync(int id, DriverProfile driverProfile)
        {
            var existing = await _context.DriverProfiles.FindAsync(id);
            if (existing == null) return false;

            existing.UserId = driverProfile.UserId;
            existing.LicenseNumber = driverProfile.LicenseNumber;
            existing.VehicleModel = driverProfile.VehicleModel;
            existing.VehiclePlate = driverProfile.VehiclePlate;
            existing.BatteryCapacity = driverProfile.BatteryCapacity;
            existing.CorporateId = driverProfile.CorporateId;

            _context.DriverProfiles.Update(existing);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var driver = await _context.DriverProfiles.FindAsync(id);
            if (driver == null) return false;

            _context.DriverProfiles.Remove(driver);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
