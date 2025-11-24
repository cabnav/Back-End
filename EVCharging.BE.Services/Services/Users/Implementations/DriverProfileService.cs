using EVCharging.BE.Common.DTOs.DriverProfiles;
using EVCharging.BE.Common.DTOs.Users;
using EVCharging.BE.DAL;
using EVCharging.BE.DAL.Entities;
using EVCharging.BE.Services.Services.Notification;
using Microsoft.EntityFrameworkCore;

namespace EVCharging.BE.Services.Services.Users.Implementations
{
    public class DriverProfileService : IDriverProfileService
    {
        private readonly EvchargingManagementContext _db;
        private readonly INotificationService _notificationService;

        public DriverProfileService(EvchargingManagementContext db, INotificationService notificationService)
        {
            _db = db;
            _notificationService = notificationService;
        }

        private static DriverProfileDTO Map(DriverProfile d) => new DriverProfileDTO
        {
            DriverId = d.DriverId,
            LicenseNumber = d.LicenseNumber ?? "",
            VehicleModel = d.VehicleModel ?? "",
            VehiclePlate = d.VehiclePlate ?? "",
            BatteryCapacity = d.BatteryCapacity,
            CorporateId = d.CorporateId,
            Status = d.Status ?? "active",
            CreatedAt = d.CreatedAt
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

            // ✅ Xác định Status
            string status;
            if (req.CorporateId.HasValue)
            {
                // Có CorporateId → Cần approval
                status = "pending";
                
                // ✅ Kiểm tra Corporate có tồn tại không
                var corporate = await _db.CorporateAccounts
                    .FirstOrDefaultAsync(c => c.CorporateId == req.CorporateId.Value);
                if (corporate == null)
                    throw new KeyNotFoundException("Doanh nghiệp không tồn tại");
            }
            else
            {
                // Không có CorporateId → Tự động active
                status = "active";
            }

            var entity = new DriverProfile
            {
                UserId = req.UserId,
                LicenseNumber = req.LicenseNumber,
                VehicleModel = req.VehicleModel,
                VehiclePlate = req.VehiclePlate,
                BatteryCapacity = req.BatteryCapacity,
                CorporateId = req.CorporateId,
                Status = status,
                CreatedAt = DateTime.UtcNow
            };
            
            _db.DriverProfiles.Add(entity);
            await _db.SaveChangesAsync();

            // ✅ Nếu có CorporateId → Gửi notification cho AdminUserId
            if (req.CorporateId.HasValue)
            {
                var corporate = await _db.CorporateAccounts
                    .FirstOrDefaultAsync(c => c.CorporateId == req.CorporateId.Value);
                
                if (corporate != null)
                {
                    await _notificationService.SendNotificationAsync(
                        corporate.AdminUserId,
                        "Driver mới xin tham gia công ty",
                        $"Driver {user.Name} (Email: {user.Email}) đã đăng ký tham gia công ty {corporate.CompanyName}. Vui lòng xác nhận.",
                        "driver_join_request",
                        entity.DriverId
                    );
                }
            }

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
            {
                var newCorporateId = req.CorporateId.Value;
                
                // ✅ Nếu CorporateId = 0 hoặc null → Xóa CorporateId (set về null)
                if (newCorporateId <= 0)
                {
                    d.CorporateId = null;
                    d.Status = "active"; // Khi không có corporate thì tự động active
                    d.ApprovedByUserId = null;
                    d.ApprovedAt = null;
                }
                else
                {
                    // ✅ Kiểm tra CorporateId có tồn tại không
                    var corporate = await _db.CorporateAccounts
                        .FirstOrDefaultAsync(c => c.CorporateId == newCorporateId);
                    if (corporate == null)
                        throw new KeyNotFoundException("Corporate không tồn tại");

                    // Nếu thay đổi CorporateId, reset status về pending
                    if (d.CorporateId != newCorporateId)
                    {
                        d.CorporateId = newCorporateId;
                        d.Status = "pending"; // Reset về pending khi đổi corporate
                        d.ApprovedByUserId = null;
                        d.ApprovedAt = null;
                    }
                    else
                    {
                        // Giữ nguyên CorporateId hiện tại (không thay đổi)
                        // Không cần update gì cả
                    }
                }
            }

            d.UpdatedAt = DateTime.UtcNow;

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