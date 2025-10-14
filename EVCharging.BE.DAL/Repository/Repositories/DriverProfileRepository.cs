using EVCharging.BE.DAL;
using EVCharging.BE.DAL.Entities;
using EVCharging.BE.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EVCharging.BE.Repositories.Repositories
{
    public class DriverProfileRepository : IDriverProfileRepository
    {
        private readonly EvchargingManagementContext _context;

        public DriverProfileRepository(EvchargingManagementContext context)
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

        public async Task AddAsync(DriverProfile entity)
        {
            await _context.DriverProfiles.AddAsync(entity);
        }

        public void Update(DriverProfile entity)
        {
            _context.DriverProfiles.Update(entity);
        }

        public void Delete(DriverProfile entity)
        {
            _context.DriverProfiles.Remove(entity);
        }

        public async Task SaveAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
