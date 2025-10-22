using EVCharging.BE.Common.DTOs.Stations; // 👉 import DTO
using EVCharging.BE.Common.Enums;
using EVCharging.BE.DAL;
using EVCharging.BE.DAL.Entities;
using EVCharging.BE.Services.DTOs;
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
                station.StationId,
                StationName = station.Name,
                TotalPoints = total,
                Available = available,
                Busy = busy,
                Maintenance = maintenance,
                Utilization = total == 0 ? 0 : Math.Round((double)busy / total * 100, 1)
            };
        }

        /// <summary>
        /// Get interactive station map data with real-time status
        /// </summary>
        public async Task<IEnumerable<InteractiveStationDTO>> GetInteractiveStationsAsync(StationFilterDTO filter)
        {
            var query = _db.ChargingStations
                .Include(s => s.ChargingPoints)
                .Where(s => s.Latitude.HasValue && s.Longitude.HasValue);

            // Apply filters
            if (!string.IsNullOrEmpty(filter.Name))
                query = query.Where(s => EF.Functions.Like(s.Name, $"%{filter.Name}%"));

            if (!string.IsNullOrEmpty(filter.Address))
                query = query.Where(s => EF.Functions.Like(s.Address, $"%{filter.Address}%"));

            if (!string.IsNullOrEmpty(filter.Operator))
                query = query.Where(s => EF.Functions.Like(s.Operator, $"%{filter.Operator}%"));

            if (!string.IsNullOrEmpty(filter.Status))
                query = query.Where(s => s.Status == filter.Status);

            var stations = await query.ToListAsync();

            var result = stations.Select(s => new InteractiveStationDTO
            {
                StationId = s.StationId,
                Name = s.Name,
                Address = s.Address,
                Latitude = s.Latitude ?? 0,
                Longitude = s.Longitude ?? 0,
                Operator = s.Operator,
                Status = s.Status ?? "Unknown",
                DistanceKm = filter.Latitude.HasValue && filter.Longitude.HasValue
                    ? Math.Round(CalculateDistance(filter.Latitude.Value, filter.Longitude.Value, s.Latitude.Value, s.Longitude.Value), 2)
                    : 0,
                GoogleMapsUrl = $"https://www.google.com/maps?q={s.Latitude},{s.Longitude}",
                TotalPoints = s.ChargingPoints.Count,
                AvailablePoints = s.ChargingPoints.Count(p => p.Status == "Available"),
                BusyPoints = s.ChargingPoints.Count(p => p.Status == "Busy" || p.Status == "in_use"),
                MaintenancePoints = s.ChargingPoints.Count(p => p.Status == "Maintenance"),
                UtilizationPercentage = s.ChargingPoints.Count == 0 ? 0 : 
                    Math.Round((double)s.ChargingPoints.Count(p => p.Status == "Busy" || p.Status == "in_use") / s.ChargingPoints.Count * 100, 1),
                ChargingPoints = s.ChargingPoints.Select(cp => new ChargingPointMapDTO
                {
                    PointId = cp.PointId,
                    ConnectorType = Enum.TryParse<ConnectorType>(cp.ConnectorType, out var connectorType) ? connectorType : ConnectorType.AC,
                    PowerOutput = cp.PowerOutput ?? 0,
                    PricePerKwh = cp.PricePerKwh,
                    Status = cp.Status ?? "Unknown",
                    CurrentPower = cp.CurrentPower ?? 0,
                    IsAvailable = cp.Status == "Available",
                    LastMaintenance = cp.LastMaintenance?.ToDateTime(TimeOnly.MinValue)
                }).ToList(),
                Pricing = CalculatePricingInfo(s.ChargingPoints)
            }).ToList();

            // Apply additional filters
            if (filter.ConnectorTypes?.Any() == true)
            {
                result = result.Where(s => s.ChargingPoints.Any(cp => filter.ConnectorTypes.Contains(cp.ConnectorType))).ToList();
            }

            if (filter.MinPowerOutput.HasValue)
            {
                result = result.Where(s => s.ChargingPoints.Any(cp => cp.PowerOutput >= filter.MinPowerOutput.Value)).ToList();
            }

            if (filter.MaxPricePerKwh.HasValue)
            {
                result = result.Where(s => s.ChargingPoints.Any(cp => cp.PricePerKwh <= filter.MaxPricePerKwh.Value)).ToList();
            }

            if (filter.IsAvailable.HasValue)
            {
                result = result.Where(s => filter.IsAvailable.Value ? s.AvailablePoints > 0 : s.AvailablePoints == 0).ToList();
            }

            if (filter.MaxDistanceKm.HasValue && filter.Latitude.HasValue && filter.Longitude.HasValue)
            {
                result = result.Where(s => s.DistanceKm <= filter.MaxDistanceKm.Value).ToList();
            }

            return result.OrderBy(s => s.DistanceKm);
        }

        /// <summary>
        /// Get real-time station status for map updates
        /// </summary>
        public async Task<object> GetRealTimeStationStatusAsync(int stationId)
        {
            var station = await _db.ChargingStations
                .Include(s => s.ChargingPoints)
                .FirstOrDefaultAsync(s => s.StationId == stationId);

            if (station == null) return null;

            var total = station.ChargingPoints.Count;
            var available = station.ChargingPoints.Count(p => p.Status == "Available");
            var busy = station.ChargingPoints.Count(p => p.Status == "Busy" || p.Status == "in_use");
            var maintenance = station.ChargingPoints.Count(p => p.Status == "Maintenance");

            return new
            {
                StationId = station.StationId,
                StationName = station.Name,
                TotalPoints = total,
                Available = available,
                Busy = busy,
                Maintenance = maintenance,
                Utilization = total == 0 ? 0 : Math.Round((double)busy / total * 100, 1),
                LastUpdated = DateTime.UtcNow,
                ChargingPoints = station.ChargingPoints.Select(cp => new
                {
                    cp.PointId,
                    cp.ConnectorType,
                    cp.PowerOutput,
                    cp.PricePerKwh,
                    cp.Status,
                    cp.CurrentPower,
                    IsAvailable = cp.Status == "Available"
                })
            };
        }

        /// <summary>
        /// Calculate pricing information including peak/off-peak rates
        /// </summary>
        private PricingInfoDTO CalculatePricingInfo(ICollection<ChargingPoint> chargingPoints)
        {
            if (!chargingPoints.Any())
                return new PricingInfoDTO();

            var basePrice = chargingPoints.Min(cp => cp.PricePerKwh);
            var peakMultiplier = 1.5m; // 50% increase during peak hours
            var offPeakMultiplier = 0.8m; // 20% discount during off-peak hours

            var now = DateTime.Now;
            var currentHour = now.Hour;
            var isPeakHour = currentHour >= 18 && currentHour <= 22; // Peak hours: 6 PM - 10 PM
            var isOffPeakHour = currentHour >= 0 && currentHour <= 6; // Off-peak hours: 12 AM - 6 AM

            var currentPrice = isPeakHour ? basePrice * peakMultiplier :
                              isOffPeakHour ? basePrice * offPeakMultiplier :
                              basePrice;

            return new PricingInfoDTO
            {
                BasePricePerKwh = basePrice,
                PeakHourPrice = basePrice * peakMultiplier,
                OffPeakPrice = basePrice * offPeakMultiplier,
                PeakHours = "18:00-22:00",
                CurrentPrice = currentPrice,
                IsPeakHour = isPeakHour,
                PriceDescription = isPeakHour ? "Peak Hour Rate" :
                                  isOffPeakHour ? "Off-Peak Rate" :
                                  "Standard Rate"
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
