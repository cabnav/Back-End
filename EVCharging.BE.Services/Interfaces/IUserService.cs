using EVCharging.BE.DAL.Entities;

namespace EVCharging.BE.Services.Interfaces
{
    public interface IUserService
    {
        Task<IEnumerable<User>> GetAllAsync();
        Task<User?> GetByIdAsync(int id);
        Task<User> CreateAsync(User user);
        Task<bool> UpdateAsync(int id, User updated);
        Task<bool> DeleteAsync(int id);
    }
}
