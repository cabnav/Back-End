using EVCharging.BE.Common.DTOs.DriverProfiles;
using EVCharging.BE.Common.DTOs.Users;

namespace EVCharging.BE.Services.Services.Users
{
    public interface IDriverProfileService
    {
        Task<IEnumerable<DriverProfileDTO>> GetAllAsync(int page = 1, int pageSize = 50);
        Task<DriverProfileDTO?> GetByIdAsync(int id);
        Task<DriverProfileDTO?> GetByUserIdAsync(int userId);

        Task<DriverProfileDTO> CreateAsync(DriverProfileCreateRequest req);
        Task<bool> UpdateAsync(int id, DriverProfileUpdateRequest req);
        Task<bool> DeleteAsync(int id);
    }
}
