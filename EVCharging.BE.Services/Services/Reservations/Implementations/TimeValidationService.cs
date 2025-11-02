using EVCharging.BE.DAL;
using EVCharging.BE.Services.Services.Background;
using Microsoft.Extensions.Options;
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
        private readonly ReservationBackgroundOptions _opt;

        public TimeValidationService(EvchargingManagementContext db, IOptions<ReservationBackgroundOptions> opt)
        {
            _db = db;
            _opt = opt.Value;
        }

        public async Task ValidateTimeSlotAsync(int pointId, DateTime startUtc, DateTime endUtc)
        {
            if (startUtc >= endUtc)
                throw new ArgumentException("Invalid time range (khoảng thời gian không hợp lệ).");

            // Không cho đặt trong quá khứ (no booking in the past)
            // Chỉ block những slot đã kết thúc hoàn toàn + buffer 5 phút để tránh edge case
            var now = DateTime.UtcNow;
            var bufferMinutes = 5; // Buffer 5 phút để tránh book quá sát giờ hiện tại
            var cutoffTime = now.AddMinutes(bufferMinutes);
            
            if (endUtc <= cutoffTime)
            {
                throw new InvalidOperationException($"Cannot book in the past. Selected time slot ends at {endUtc:yyyy-MM-dd HH:mm}, but current time is {now:yyyy-MM-dd HH:mm}. Please select a future time slot.");
            }

            // Không cho đặt khi đã quá BookingCutoff kể từ start (ví dụ 15 phút)
            if (now > startUtc.AddMinutes(_opt.BookingCutoffMinutes))
            {
                var minutesPassed = (int)(now - startUtc).TotalMinutes;
                throw new InvalidOperationException(
                    $"Cannot book this time slot. Booking for this slot closed {minutesPassed - _opt.BookingCutoffMinutes} minutes ago " +
                    $"(current time: {now:yyyy-MM-dd HH:mm} UTC, slot start: {startUtc:yyyy-MM-dd HH:mm} UTC). " +
                    $"You can only book within {_opt.BookingCutoffMinutes} minutes after the slot start time.");
            }

            // Kiểm tra point có tồn tại (check charging point existence)
            var pointExists = await _db.ChargingPoints.AnyAsync(p => p.PointId == pointId);
            if (!pointExists)
                throw new KeyNotFoundException("Charging point not found (không tìm thấy điểm sạc).");

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
