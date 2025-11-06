using EVCharging.BE.DAL;
using EVCharging.BE.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using CP = EVCharging.BE.Common.DTOs.Stations;
using System.Linq;

namespace EVCharging.BE.Services.Services.Charging.Implementations
{
    public class ChargingPointService : IChargingPointService
    {
        private readonly EvchargingManagementContext _db;
        public ChargingPointService(EvchargingManagementContext db) => _db = db;

        // ===== READ =====
        public async Task<IEnumerable<CP.ChargingPointDTO>> GetAllAsync()
        {
            return await _db.ChargingPoints
                .AsNoTracking()
                .Include(p => p.Station)
                .Select(p => new CP.ChargingPointDTO
                {
                    PointId = p.PointId,
                    StationId = p.StationId,
                    ConnectorType = p.ConnectorType,
                    Status = p.Status,
                    PowerOutput = p.PowerOutput,
                    PricePerKwh = p.PricePerKwh,
                    CurrentPower = p.CurrentPower,
                    LastMaintenance = p.LastMaintenance,
                    QrCode = p.QrCode,
                    StationName = p.Station.Name,
                    StationAddress = p.Station.Address
                })
                .ToListAsync();
        }

        public async Task<CP.ChargingPointDTO?> GetByIdAsync(int id)
        {
            return await _db.ChargingPoints
                .AsNoTracking()
                .Include(p => p.Station)
                .Where(p => p.PointId == id)
                .Select(p => new CP.ChargingPointDTO
                {
                    PointId = p.PointId,
                    StationId = p.StationId,
                    ConnectorType = p.ConnectorType,
                    Status = p.Status,
                    PowerOutput = p.PowerOutput,
                    PricePerKwh = p.PricePerKwh,
                    CurrentPower = p.CurrentPower,
                    LastMaintenance = p.LastMaintenance,
                    QrCode = p.QrCode,
                    StationName = p.Station.Name,
                    StationAddress = p.Station.Address
                })
                .FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<CP.ChargingPointDTO>> GetAvailableAsync()
        {
            return await _db.ChargingPoints
                .AsNoTracking()
                .Include(p => p.Station)
                .Where(p => p.Status!.ToLower() == "available")
                .Select(p => new CP.ChargingPointDTO
                {
                    PointId = p.PointId,
                    StationId = p.StationId,
                    ConnectorType = p.ConnectorType,
                    Status = p.Status,
                    PowerOutput = p.PowerOutput,
                    PricePerKwh = p.PricePerKwh,
                    CurrentPower = p.CurrentPower,
                    LastMaintenance = p.LastMaintenance,
                    QrCode = p.QrCode,
                    StationName = p.Station.Name,
                    StationAddress = p.Station.Address
                })
                .ToListAsync();
        }

        public async Task<IEnumerable<CP.ChargingPointDTO>> GetByStationAsync(int stationId)
        {
            return await _db.ChargingPoints
                .AsNoTracking()
                .Include(p => p.Station)
                .Where(p => p.StationId == stationId)
                .Select(p => new CP.ChargingPointDTO
                {
                    PointId = p.PointId,
                    StationId = p.StationId,
                    ConnectorType = p.ConnectorType,
                    Status = p.Status,
                    PowerOutput = p.PowerOutput,
                    PricePerKwh = p.PricePerKwh,
                    CurrentPower = p.CurrentPower,
                    LastMaintenance = p.LastMaintenance,
                    QrCode = p.QrCode,
                    StationName = p.Station.Name,
                    StationAddress = p.Station.Address
                })
                .ToListAsync();
        }

        // ===== STATUS =====
         public async Task<CP.ChargingPointDTO?> UpdateStatusAsync(int id, string newStatus)
 {
     var entity = await _db.ChargingPoints.FirstOrDefaultAsync(p => p.PointId == id);
     if (entity == null) return null;

     entity.Status = newStatus;
     await _db.SaveChangesAsync();

     return await GetByIdAsync(id);
 }

        // ===== CREATE =====
        public async Task<CP.ChargingPointDTO> CreateAsync(CP.ChargingPointCreateRequest req)
        {
            var station = await _db.ChargingStations.FirstOrDefaultAsync(s => s.StationId == req.StationId);
            if (station == null)
                throw new InvalidOperationException($"Station ID {req.StationId} not found.");

            var entity = new ChargingPoint
            {
                StationId = req.StationId,
                ConnectorType = req.ConnectorType,
                Status = req.Status ?? "Available",
                PowerOutput = req.PowerOutput,
                PricePerKwh = req.PricePerKwh,
                CurrentPower = req.CurrentPower,
                LastMaintenance = req.LastMaintenance,
                QrCode = req.QrCode
            };

            _db.ChargingPoints.Add(entity);
            await _db.SaveChangesAsync();

            return await GetByIdAsync(entity.PointId)
                   ?? new CP.ChargingPointDTO { PointId = entity.PointId, StationId = entity.StationId };
        }

        // ===== UPDATE =====
        public async Task<bool> UpdateAsync(int id, CP.ChargingPointUpdateRequest req)
        {
            var entity = await _db.ChargingPoints.FirstOrDefaultAsync(p => p.PointId == id);
            if (entity == null) return false;

            if (req.ConnectorType != null) entity.ConnectorType = req.ConnectorType;
            if (req.Status != null) entity.Status = req.Status;
            if (req.PowerOutput.HasValue) entity.PowerOutput = req.PowerOutput;
            if (req.PricePerKwh.HasValue) entity.PricePerKwh = req.PricePerKwh.Value;
            if (req.CurrentPower.HasValue) entity.CurrentPower = req.CurrentPower;
            if (req.LastMaintenance.HasValue) entity.LastMaintenance = req.LastMaintenance.Value;
            if (req.QrCode != null) entity.QrCode = req.QrCode;

            await _db.SaveChangesAsync();
            return true;
        }

        // ===== DELETE =====
        public async Task<bool> DeleteAsync(int id)
        {
            var entity = await _db.ChargingPoints.FirstOrDefaultAsync(p => p.PointId == id);
            if (entity == null) return false;

            _db.ChargingPoints.Remove(entity);
            await _db.SaveChangesAsync();
            return true;
        }
    }
}
