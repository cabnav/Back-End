using EVCharging.BE.DAL;
using EVCharging.BE.DAL.Entities;
using EVCharging.BE.Services.DTOs;
using Microsoft.EntityFrameworkCore;

namespace EVCharging.BE.Services.Services.Implementations
{
    public class ChargingStationService : IChargingStationService
    {
        private readonly EvchargingManagementContext _db;

        public ChargingStationService(EvchargingManagementContext db)
        {
            _db = db;
        }

        public async Task<IEnumerable<ChargingStation>> GetAllAsync()
        {
            return await _db.ChargingStations.Include(s => s.ChargingPoints).ToListAsync();
        }

        public async Task<ChargingStation?> GetByIdAsync(int id)
        {
            return await _db.ChargingStations
                .Include(s => s.ChargingPoints)
                .FirstOrDefaultAsync(s => s.StationId == id);
        }

        // ✅ Hàm 1: Tìm trạm gần vị trí GPS
        public async Task<IEnumerable<ChargingStation>> GetNearbyStationsAsync(double lat, double lon, double radiusKm)
        {
            var all = await _db.ChargingStations.Include(s => s.ChargingPoints).ToListAsync();
            return all.Where(s => CalculateDistance(lat, lon, s.Latitude ?? 0, s.Longitude ?? 0) <= radiusKm);
        }

        // ✅ Hàm 2: Tìm kiếm theo điều kiện
        public async Task<IEnumerable<ChargingStation>> SearchStationsAsync(StationSearchDTO filter)
        {
            var query = _db.ChargingStations.AsQueryable();

            if (!string.IsNullOrEmpty(filter.Name))
                query = query.Where(s => EF.Functions.Like(s.Name, $"%{filter.Name}%"));

            if (!string.IsNullOrEmpty(filter.Address))
                query = query.Where(s => EF.Functions.Like(s.Address, $"%{filter.Address}%"));

            if (!string.IsNullOrEmpty(filter.Operator))
                query = query.Where(s => EF.Functions.Like(s.Operator, $"%{filter.Operator}%"));

            if (filter.Latitude.HasValue && filter.Longitude.HasValue && filter.MaxDistanceKm.HasValue)
            {
                var all = await query.Include(s => s.ChargingPoints).ToListAsync();
                return all.Where(s =>
                    CalculateDistance(filter.Latitude.Value, filter.Longitude.Value, s.Latitude ?? 0, s.Longitude ?? 0)
                    <= filter.MaxDistanceKm.Value);
            }

            return await query.Include(s => s.ChargingPoints).ToListAsync();
        }

        // ✅ Hàm 3: Trạng thái tổng hợp của trạm
        public async Task<object?> GetStationStatusAsync(int stationId)
        {
            var station = await _db.ChargingStations
                .Include(s => s.ChargingPoints)
                .FirstOrDefaultAsync(s => s.StationId == stationId);

            if (station == null) return null;

            int total = station.ChargingPoints.Count;
            int available = station.ChargingPoints.Count(p => p.Status == "Available");
            int busy = station.ChargingPoints.Count(p => p.Status == "Busy");
            int maintenance = station.ChargingPoints.Count(p => p.Status == "Maintenance");

            return new
            {
                StationId = station.StationId,
                StationName = station.Name,
                TotalPoints = total,
                Available = available,
                Busy = busy,
                Maintenance = maintenance,
                Utilization = total == 0 ? 0 : Math.Round((double)busy / total * 100, 1)
            };
        }

        // Hàm tính khoảng cách (km)
        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371;
            var dLat = (lat2 - lat1) * Math.PI / 180;
            var dLon = (lon2 - lon1) * Math.PI / 180;
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }
    }
}
