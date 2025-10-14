using EVCharging.BE.DAL.Entities;

namespace EVCharging.BE.Repositories.Interfaces
{
    public interface IDriverProfileRepository
    {
        Task<IEnumerable<DriverProfile>> GetAllAsync();
        Task<DriverProfile?> GetByIdAsync(int id);
        Task AddAsync(DriverProfile entity);
        void Update(DriverProfile entity);
        void Delete(DriverProfile entity);
        Task SaveAsync();
    }
}
