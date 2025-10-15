using EVCharging.BE.DAL.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EVCharging.BE.Services.Services
{
    public interface IDriverProfileService
    {
        Task<IEnumerable<DriverProfile>> GetAllAsync();
        Task<DriverProfile?> GetByIdAsync(int id);
        Task<DriverProfile> CreateAsync(DriverProfile driverProfile);
        Task<bool> UpdateAsync(int id, DriverProfile driverProfile); 
        Task<bool> DeleteAsync(int id);
    }
}
