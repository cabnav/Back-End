using EVCharging.BE.DAL;
using EVCharging.BE.DAL.Entities;
using EVCharging.BE.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EVCharging.BE.Services.Implementations
{
    public class UserService : IUserService
    {
        private readonly EvchargingManagementContext _db;

        public UserService(EvchargingManagementContext db)
        {
            _db = db;
        }

        public async Task<IEnumerable<User>> GetAllAsync()
            => await _db.Users.ToListAsync();

        public async Task<User?> GetByIdAsync(int id)
            => await _db.Users.FindAsync(id);

        public async Task<User> CreateAsync(User user)
        {
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
            return user;
        }

        public async Task<bool> UpdateAsync(int id, User updated)
        {
            if (id != updated.UserId) return false;

            _db.Users.Update(updated);
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null) return false;

            _db.Users.Remove(user);
            await _db.SaveChangesAsync();
            return true;
        }
    }
}
