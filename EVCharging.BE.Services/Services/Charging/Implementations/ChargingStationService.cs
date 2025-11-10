using System;
using EVCharging.BE.Common.DTOs.Stations;
using EVCharging.BE.Common.DTOs.Stations.EVCharging.BE.Common.DTOs.Stations;
using EVCharging.BE.Common.Enums;
using EVCharging.BE.DAL;
using EVCharging.BE.DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace EVCharging.BE.Services.Services.Charging.Implementations
{
    public class ChargingStationService : IChargingStationService
    {
        private readonly EvchargingManagementContext _db;

        public ChargingStationService(EvchargingManagementContext db)
        {
            _db = db;
        }

        // ===== CRUD =====
        public async Task<IEnumerable<StationDTO>> GetAllAsync()
        {
            var list = await _db.ChargingStations.AsNoTracking().ToListAsync();
            return list.Select(ToDTO);
        }

        public async Task<StationDTO?> GetByIdAsync(int id)
        {
            var s = await _db.ChargingStations.AsNoTracking()
                        .FirstOrDefaultAsync(x => x.StationId == id);
            return s == null ? null : ToDTO(s);
        }

        public async Task<StationDTO> CreateAsync(StationCreateRequest req)
        {
            var entity = new ChargingStation
            {
                Name = req.Name,
                Address = req.Address,
                Latitude = req.Latitude,
                Longitude = req.Longitude,
                Operator = req.Operator,
                Status = req.Status
            };

            _db.ChargingStations.Add(entity);
            await _db.SaveChangesAsync();
            return ToDTO(entity);
        }

        public async Task<StationDTO?> UpdateAsync(int id, StationUpdateRequest req)
        {
            var s = await _db.ChargingStations.FirstOrDefaultAsync(x => x.StationId == id);
            if (s == null) return null;

            s.Name = req.Name ?? s.Name;
            s.Address = req.Address ?? s.Address;
            s.Latitude = req.Latitude ?? s.Latitude;
            s.Longitude = req.Longitude ?? s.Longitude;
            s.Operator = req.Operator ?? s.Operator;
            s.Status = req.Status ?? s.Status;

            await _db.SaveChangesAsync();
            return ToDTO(s);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var s = await _db.ChargingStations.FirstOrDefaultAsync(x => x.StationId == id);
            if (s == null) return false;

            _db.ChargingStations.Remove(s);
            await _db.SaveChangesAsync();
            return true;
        }

        private static StationDTO ToDTO(ChargingStation s) => new StationDTO
        {
            StationId = s.StationId,
            Name = s.Name,
            Address = s.Address,
            Latitude = s.Latitude,
            Longitude = s.Longitude,
            Operator = s.Operator,
            Status = s.Status,
            TotalPoints = s.TotalPoints,
            AvailablePoints = s.AvailablePoints
        };

        // ===== SEARCH =====
        public async Task<IEnumerable<InteractiveStationDTO>> GetInteractiveStationsAsync(StationFilterDTO filter)
        {
            var query = _db.ChargingStations
                .Include(s => s.ChargingPoints)
                .Where(s => s.Latitude.HasValue && s.Longitude.HasValue);

            if (!string.IsNullOrWhiteSpace(filter.Name))
                query = query.Where(s => EF.Functions.Like(s.Name, $"%{filter.Name}%"));
            if (!string.IsNullOrWhiteSpace(filter.Address))
                query = query.Where(s => EF.Functions.Like(s.Address, $"%{filter.Address}%"));
            if (!string.IsNullOrWhiteSpace(filter.Operator))
                query = query.Where(s => EF.Functions.Like(s.Operator, $"%{filter.Operator}%"));
            if (!string.IsNullOrWhiteSpace(filter.Status))
                query = query.Where(s => s.Status != null && s.Status.ToLower() == filter.Status.ToLower());

            var stations = await query.AsNoTracking().ToListAsync();

            var result = stations.Select(s => new InteractiveStationDTO
            {
                StationId = s.StationId,
                Name = s.Name,
                Address = s.Address,
                Latitude = s.Latitude ?? 0,
                Longitude = s.Longitude ?? 0,
                Operator = s.Operator,
                Status = s.Status ?? "Unknown",
                TotalPoints = s.ChargingPoints.Count,
                AvailablePoints = s.ChargingPoints.Count(p => p.Status == "Available"),
                BusyPoints = s.ChargingPoints.Count(p => p.Status == "Busy" || p.Status == "in_use"),
                MaintenancePoints = s.ChargingPoints.Count(p => p.Status == "Maintenance"),
                GoogleMapsUrl = $"https://www.google.com/maps?q={s.Latitude},{s.Longitude}",
                ChargingPoints = s.ChargingPoints.Select(cp => new ChargingPointMapDTO
                {
                    PointId = cp.PointId,
                    ConnectorType = ConnectorType.AC,
                    PowerOutput = cp.PowerOutput ?? 0,
                    PricePerKwh = cp.PricePerKwh,
                    Status = cp.Status ?? "Unknown",
                    CurrentPower = cp.CurrentPower ?? 0,
                    IsAvailable = cp.Status == "Available",
                    LastMaintenance = cp.LastMaintenance?.ToDateTime(TimeOnly.MinValue)
                }).ToList()
            });

            // Filter khoảng cách
            if (filter.MaxDistanceKm.HasValue && filter.Latitude.HasValue && filter.Longitude.HasValue)
            {
                double Haversine(double la1, double lo1, double la2, double lo2)
                {
                    const double R = 6371;
                    var dLat = (la2 - la1) * Math.PI / 180;
                    var dLon = (lo2 - lo1) * Math.PI / 180;
                    var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                             Math.Cos(la1 * Math.PI / 180) * Math.Cos(la2 * Math.PI / 180) *
                             Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
                    return 2 * R * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
                }

                result = result.Select(r =>
                {
                    r.DistanceKm = Haversine(filter.Latitude.Value, filter.Longitude.Value, r.Latitude, r.Longitude);
                    return r;
                })
                .Where(r => r.DistanceKm <= filter.MaxDistanceKm.Value)
                .OrderBy(r => r.DistanceKm)
                .ToList();
            }

            return result;
        }

        public Task<IEnumerable<InteractiveStationDTO>> SearchStationsAsync(StationFilterDTO filter)
            => GetInteractiveStationsAsync(filter);

        // ===== REAL-TIME =====
        public async Task<object?> GetRealTimeStationStatusAsync(int stationId)
        {
            var s = await _db.ChargingStations
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.StationId == stationId);

            if (s == null) return null;

            // Query trực tiếp từ ChargingPoints để đảm bảo load đúng dữ liệu
            var points = await _db.ChargingPoints
                .AsNoTracking()
                .Where(p => p.StationId == stationId)
                .ToListAsync();

            var total = points.Count;
            var available = points.Count(p => p.Status != null && p.Status.Equals("Available", StringComparison.OrdinalIgnoreCase));
            var busy = points.Count(p => p.Status != null && (p.Status.Equals("Busy", StringComparison.OrdinalIgnoreCase) || p.Status.Equals("in_use", StringComparison.OrdinalIgnoreCase)));
            var maintenance = points.Count(p => p.Status != null && p.Status.Equals("Maintenance", StringComparison.OrdinalIgnoreCase));

            return new
            {
                s.StationId,
                StationName = s.Name,
                TotalPoints = total,
                Available = available,
                Busy = busy,
                Maintenance = maintenance,
                Utilization = total == 0 ? 0 : Math.Round((double)busy / total * 100, 1),
                LastUpdated = DateTime.UtcNow
            };
        }

    }
}
