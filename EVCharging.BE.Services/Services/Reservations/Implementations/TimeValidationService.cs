using EVCharging.BE.DAL;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EVCharging.BE.Services.Services.Reservations.Implementations
{
    /// <summary>
    /// Validate time slot (kiểm tra khung giờ) trước khi tạo đặt chỗ
    /// </summary>
    public class TimeValidationService : ITimeValidationService
    {
        private readonly EvchargingManagementContext _db;

        public TimeValidationService(EvchargingManagementContext db)
        {
            _db = db;
        }

        public async Task ValidateTimeSlotAsync(int pointId, DateTime startUtc, DateTime endUtc)
        {
            if (startUtc >= endUtc)
                throw new ArgumentException("Invalid time range (khoảng thời gian không hợp lệ).");

            // Không cho đặt trong quá khứ (no booking in the past)
            if (startUtc < DateTime.UtcNow)
                throw new InvalidOperationException("Cannot book in the past (không thể đặt trong quá khứ).");

            // Kiểm tra point có tồn tại và có status hợp lệ (check charging point existence and status)
            var chargingPoint = await _db.ChargingPoints
                .Include(cp => cp.Station)
                .FirstOrDefaultAsync(p => p.PointId == pointId);

            if (chargingPoint == null)
                throw new KeyNotFoundException("Charging point not found (không tìm thấy điểm sạc).");

            // Kiểm tra status của charging point
            if (chargingPoint.Status != "available")
                throw new InvalidOperationException($"Charging point is not available (điểm sạc không khả dụng). Current status: {chargingPoint.Status ?? "null"}");

            // Kiểm tra status của station
            if (chargingPoint.Station == null || chargingPoint.Station.Status != "active")
                throw new InvalidOperationException("Charging station is not active (trạm sạc không hoạt động).");

            // Kiểm tra trùng giờ (overlap) trên cùng point
            // Overlap khi: NOT (newEnd <= start OR newStart >= end)
            var overlap = await _db.Reservations
                .Where(r => r.PointId == pointId && (r.Status == "booked" || r.Status == "completed"))
                .AnyAsync(r => !(endUtc <= r.StartTime || startUtc >= r.EndTime));

            if (overlap)
                throw new InvalidOperationException("Time slot not available (khung giờ đã có người đặt).");
        }
    }
}
