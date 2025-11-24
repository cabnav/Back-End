using EVCharging.BE.DAL;
using EVCharging.BE.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Org.BouncyCastle.Crypto;
using System.Linq;
using CP = EVCharging.BE.Common.DTOs.Stations;

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

     var oldStatus = entity.Status;
     entity.Status = newStatus;

     // ✅ Tự động cập nhật last_maintenance khi chuyển từ maintenance về available
     if (oldStatus?.ToLower() == "maintenance" && newStatus.ToLower() == "available")
     {
         entity.LastMaintenance = DateOnly.FromDateTime(DateTime.UtcNow);
         Console.WriteLine($"[UpdateStatusAsync] Auto-updated last_maintenance to {entity.LastMaintenance} for point {id} (maintenance -> available)");
     }

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
                Status = req.Status ?? "available",
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

            var oldStatus = entity.Status;

            if (req.ConnectorType != null) entity.ConnectorType = req.ConnectorType;
            if (req.Status != null) entity.Status = req.Status;
            if (req.PowerOutput.HasValue) entity.PowerOutput = req.PowerOutput;
            if (req.PricePerKwh.HasValue) entity.PricePerKwh = req.PricePerKwh.Value;
            if (req.CurrentPower.HasValue) entity.CurrentPower = req.CurrentPower;
            if (req.QrCode != null) entity.QrCode = req.QrCode;

            // ✅ Tự động cập nhật last_maintenance khi chuyển từ maintenance về available
            // Chỉ auto-update nếu không có LastMaintenance được truyền vào request
            if (req.Status != null && oldStatus?.ToLower() == "maintenance" && req.Status.ToLower() == "available")
            {
                if (!req.LastMaintenance.HasValue) // Chỉ auto-update nếu không có giá trị từ request
                {
                    entity.LastMaintenance = DateOnly.FromDateTime(DateTime.UtcNow);
                    Console.WriteLine($"[UpdateAsync] Auto-updated last_maintenance to {entity.LastMaintenance} for point {id} (maintenance -> available)");
                }
            }
            else if (req.LastMaintenance.HasValue)
            {
                // Nếu có LastMaintenance trong request, dùng giá trị đó
                entity.LastMaintenance = req.LastMaintenance.Value;
            }

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

        public async Task<CP.ChargingPointDTO?> UpdatePriceAsync(int id, decimal newPricePerKwh)
        {
            // 1. Tìm Entity (Mô hình Database) theo ID
            var entity = await _db.ChargingPoints.FirstOrDefaultAsync(p => p.PointId == id);

            // 2. Kiểm tra nếu không tìm thấy
            if (entity == null)
            {
                return null; // Controller sẽ trả 404
            }

            // 3. Cập nhật trường giá tiền (PricePerKwh)
            entity.PricePerKwh = newPricePerKwh;

            // 4. Lưu thay đổi vào Database
            await _db.SaveChangesAsync();

            // 5. Trả về DTO đã cập nhật bằng cách gọi lại GetByIdAsync
            return await GetByIdAsync(id);
        }

    }
}
