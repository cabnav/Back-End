using EVCharging.BE.Common.DTOs.DriverProfiles;
using EVCharging.BE.Common.DTOs.Users;
using EVCharging.BE.DAL;
using EVCharging.BE.DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace EVCharging.BE.Services.Services.Users.Implementations
{
    public class DriverProfileService : IDriverProfileService
    {
        private readonly EvchargingManagementContext _db;
        public DriverProfileService(EvchargingManagementContext db) { _db = db; }

        private static DriverProfileDTO Map(DriverProfile d) => new DriverProfileDTO
        {
            DriverId = d.DriverId,
            LicenseNumber = d.LicenseNumber ?? "",
            VehicleModel = d.VehicleModel ?? "",
            VehiclePlate = d.VehiclePlate ?? "",
            BatteryCapacity = d.BatteryCapacity
        };

        public async Task<IEnumerable<DriverProfileDTO>> GetAllAsync(int page = 1, int pageSize = 50)
        {
            if (page < 1) page = 1;
            if (pageSize <= 0 || pageSize > 200) pageSize = 50;

            var list = await _db.DriverProfiles
                                .AsNoTracking()
                                .OrderBy(x => x.UserId)
                                .Skip((page - 1) * pageSize).Take(pageSize)
                                .ToListAsync();
            return list.Select(Map);
        }

        public async Task<DriverProfileDTO?> GetByIdAsync(int id)
        {
            var d = await _db.DriverProfiles.AsNoTracking()
                                            .FirstOrDefaultAsync(x => x.UserId == id);
            return d == null ? null : Map(d);
        }

        public async Task<DriverProfileDTO?> GetByUserIdAsync(int userId)
        {
            var d = await _db.DriverProfiles.AsNoTracking()
                                            .FirstOrDefaultAsync(x => x.UserId == userId);
            return d == null ? null : Map(d);
        }

        public async Task<DriverProfileDTO> CreateAsync(DriverProfileCreateRequest req)
        {
            var user = await _db.Users.FindAsync(req.UserId)
                       ?? throw new KeyNotFoundException("User không tồn tại");

            var driverProfile = await _db.DriverProfiles.FirstOrDefaultAsync(x => x.UserId == req.UserId);
            if (driverProfile != null)
                throw new InvalidOperationException("User đã có DriverProfile");

            // Validate BatteryCapacity > 0
            if (req.BatteryCapacity.HasValue && req.BatteryCapacity.Value <= 0)
                throw new ArgumentException("BatteryCapacity must be greater than 0");

            var entity = new DriverProfile
            {
                UserId = req.UserId,
                LicenseNumber = req.LicenseNumber,
                VehicleModel = req.VehicleModel,
                VehiclePlate = req.VehiclePlate,
                BatteryCapacity = req.BatteryCapacity,
                CorporateId = req.CorporateId
            };
            _db.DriverProfiles.Add(entity);
            await _db.SaveChangesAsync();

            return Map(entity);
        }

        public async Task<bool> UpdateAsync(int userId, DriverProfileUpdateRequest req)
        {
            var d = await _db.DriverProfiles.FirstOrDefaultAsync(x => x.UserId == userId);
            if (d == null)
                return false; // Không có profile cho user này

            // Validate BatteryCapacity > 0 nếu có value
            if (req.BatteryCapacity.HasValue && req.BatteryCapacity.Value <= 0)
                throw new ArgumentException("BatteryCapacity must be greater than 0");

            // Cập nhật các trường có giá trị hợp lệ
            if (!string.IsNullOrWhiteSpace(req.LicenseNumber))
                d.LicenseNumber = req.LicenseNumber;

            if (!string.IsNullOrWhiteSpace(req.VehicleModel))
                d.VehicleModel = req.VehicleModel;

            if (!string.IsNullOrWhiteSpace(req.VehiclePlate))
                d.VehiclePlate = req.VehiclePlate;

            if (req.BatteryCapacity.HasValue)
                d.BatteryCapacity = req.BatteryCapacity.Value;

            if (req.CorporateId.HasValue)
                d.CorporateId = req.CorporateId.Value;

            // Gợi ý thêm: cập nhật timestamp nếu có cột UpdatedAt
            // d.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var d = await _db.DriverProfiles.FindAsync(id);
            if (d == null) return false;

            _db.DriverProfiles.Remove(d);
            await _db.SaveChangesAsync();
            return true;
        }
    }
}