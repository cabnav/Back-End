using EVCharging.BE.DAL;
using EVCharging.BE.DAL.Entities;
using EVCharging.BE.Common.DTOs.Stations;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace EVCharging.BE.Services.Services.Charging.Implementations
{
    public class ChargingPointService : IChargingPointService
    {
        private readonly EvchargingManagementContext _db;

        public ChargingPointService(EvchargingManagementContext db)
        {
            _db = db;
        }

        public async Task<IEnumerable<ChargingPointDTO>> GetAllAsync()
        {
            return await _db.ChargingPoints
                .Select(p => ToDTO(p))
                .ToListAsync();
        }

        public async Task<ChargingPointDTO?> GetByIdAsync(int id)
        {
            var entity = await _db.ChargingPoints.FindAsync(id);
            return entity == null ? null : ToDTO(entity);
        }

        public async Task<IEnumerable<ChargingPointDTO>> GetAvailableAsync()
        {
            return await _db.ChargingPoints
                .Where(p => p.Status == "Available")
                .Select(p => new ChargingPointDTO
                {
                    PointId = p.PointId,
                    StationId = p.StationId,
                    ConnectorType = p.ConnectorType ?? string.Empty,
                    PowerOutput = p.PowerOutput ?? 0,
                    PricePerKwh = p.PricePerKwh,
                    Status = p.Status ?? "Unknown",
                    QrCode = p.QrCode ?? string.Empty,
                    CurrentPower = (decimal)(p.CurrentPower ?? 0),
                    LastMaintenance = p.LastMaintenance.HasValue
                        ? p.LastMaintenance.Value.ToDateTime(TimeOnly.MinValue)
                        : null
                })
                .ToListAsync();
        }
        public async Task<IEnumerable<ChargingPointDTO>> GetByStationAsync(int stationId)
        {
            return await _db.ChargingPoints
                .Where(p => p.StationId == stationId)
                .Select(p => new ChargingPointDTO
                {
                    PointId = p.PointId,
                    StationId = p.StationId,
                    ConnectorType = p.ConnectorType ?? string.Empty,
                    PowerOutput = p.PowerOutput ?? 0,
                    PricePerKwh = p.PricePerKwh,
                    Status = p.Status ?? "Unknown",
                    QrCode = p.QrCode ?? string.Empty,
                    CurrentPower = (decimal)(p.CurrentPower ?? 0),
                    LastMaintenance = p.LastMaintenance.HasValue
                        ? p.LastMaintenance.Value.ToDateTime(TimeOnly.MinValue)
                        : null
                })
                .ToListAsync();
        }

        public async Task<ChargingPointDTO?> UpdateStatusAsync(int id, string newStatus)
        {
            // ✅ Validate status values
            var validStatuses = new[] { "available", "in_use", "maintenance", "occupied", "out_of_order" };
            if (!validStatuses.Contains(newStatus?.ToLower()))
            {
                throw new ArgumentException(
                    $"Invalid status value. Allowed values are: {string.Join(", ", validStatuses)}");
            }

            var point = await _db.ChargingPoints.FindAsync(id);
            if (point == null)
                return null;

            point.Status = newStatus.ToLower();
            await _db.SaveChangesAsync();

            return ToDTO(point);
        }

        // ✅ Helper: Chuyển Entity → DTO
        private ChargingPointDTO ToDTO(ChargingPoint p)
        {
            return new ChargingPointDTO
            {
                PointId = p.PointId,
                StationId = p.StationId,
                ConnectorType = p.ConnectorType ?? string.Empty,
                PowerOutput = p.PowerOutput ?? 0, // ép nullable int
                PricePerKwh = p.PricePerKwh,
                Status = p.Status ?? "Unknown",
                QrCode = p.QrCode ?? string.Empty,
                CurrentPower = (decimal)(p.CurrentPower ?? 0), // ép double? → decimal
                LastMaintenance = p.LastMaintenance.HasValue
                    ? p.LastMaintenance.Value.ToDateTime(TimeOnly.MinValue)
                    : null
            };
        }
    }
}
