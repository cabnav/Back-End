using EVCharging.BE.Common.DTOs.Stations; // 👉 import DTO
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

        // ✅ Hàm 1: Lấy toàn bộ
        public async Task<IEnumerable<ChargingStation>> GetAllAsync()
        {
            return await _db.ChargingStations.Include(s => s.ChargingPoints).ToListAsync();
        }

        // ✅ Hàm 2: Lấy theo ID
        public async Task<ChargingStation?> GetByIdAsync(int id)
        {
            return await _db.ChargingStations
                .Include(s => s.ChargingPoints)
                .FirstOrDefaultAsync(s => s.StationId == id);
        }

        // ✅ Hàm 3: Tìm trạm gần vị trí GPS
        public async Task<IEnumerable<StationResultDTO>> GetNearbyStationsAsync(double lat, double lon, double radiusKm)
        {
            var all = await _db.ChargingStations
                .Where(s => s.Latitude.HasValue && s.Longitude.HasValue)
                .ToListAsync();

            var nearby = all
                .Select(s => new
                {
                    Station = s,
                    Distance = CalculateDistance(lat, lon, s.Latitude.Value, s.Longitude.Value)
                })
                .Where(x => x.Distance <= radiusKm)
                .OrderBy(x => x.Distance)
                .Select(x => new StationResultDTO
                {
                    StationId = x.Station.StationId,
                    Name = x.Station.Name,
                    Address = x.Station.Address,
                    Latitude = x.Station.Latitude ?? 0,
                    Longitude = x.Station.Longitude ?? 0,
                    Operator = x.Station.Operator,
                    Status = x.Station.Status,
                    DistanceKm = Math.Round(x.Distance, 2),
                    GoogleMapsUrl = $"https://www.google.com/maps?q={x.Station.Latitude},{x.Station.Longitude}"
                })
                .ToList();

            return nearby;
        }

        // ✅ Hàm 4: Tìm kiếm nâng cao
        public async Task<IEnumerable<StationResultDTO>> SearchStationsAsync(StationSearchDTO filter)
        {
            var query = _db.ChargingStations.AsQueryable();

            if (!string.IsNullOrEmpty(filter.Name))
                query = query.Where(s => EF.Functions.Like(s.Name, $"%{filter.Name}%"));

            if (!string.IsNullOrEmpty(filter.Address))
                query = query.Where(s => EF.Functions.Like(s.Address, $"%{filter.Address}%"));

            if (!string.IsNullOrEmpty(filter.Operator))
                query = query.Where(s => EF.Functions.Like(s.Operator, $"%{filter.Operator}%"));

            var list = await query.Where(s => s.Latitude.HasValue && s.Longitude.HasValue).ToListAsync();

            var result = list.Select(s => new StationResultDTO
            {
                StationId = s.StationId,
                Name = s.Name,
                Address = s.Address,
                Latitude = s.Latitude ?? 0,
                Longitude = s.Longitude ?? 0,
                Operator = s.Operator,
                Status = s.Status,
                DistanceKm = filter.Latitude.HasValue && filter.Longitude.HasValue
                    ? Math.Round(CalculateDistance(filter.Latitude.Value, filter.Longitude.Value, s.Latitude.Value, s.Longitude.Value), 2)
                    : 0,
                GoogleMapsUrl = $"https://www.google.com/maps?q={s.Latitude},{s.Longitude}"
            })
            .OrderBy(x => x.DistanceKm)
            .ToList();

            // Nếu filter.MaxDistanceKm có giá trị thì lọc thêm
            if (filter.MaxDistanceKm.HasValue)
                result = result.Where(r => r.DistanceKm <= filter.MaxDistanceKm.Value).ToList();

            return result;
        }

        // ✅ Hàm 5: Trạng thái tổng hợp của trạm
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

        // 🧭 Hàm tính khoảng cách (km)
        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371; // km
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
