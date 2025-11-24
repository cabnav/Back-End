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

            // ✅ Giới hạn đặt trước tối đa 1 tuần (7 ngày)
            const int MAX_BOOKING_ADVANCE_DAYS = 7;
            var maxBookingDate = now.AddDays(MAX_BOOKING_ADVANCE_DAYS);
            
            if (startUtc > maxBookingDate)
            {
                var daysAhead = (startUtc - now).TotalDays;
                throw new InvalidOperationException(
                    $"Không thể đặt chỗ quá 1 tuần trước. " +
                    $"Thời gian đặt chỗ: {startUtc:yyyy-MM-dd HH:mm} UTC, " +
                    $"Thời gian hiện tại: {now:yyyy-MM-dd HH:mm} UTC, " +
                    $"Bạn đang đặt trước {daysAhead:F1} ngày. " +
                    $"Chỉ cho phép đặt trước tối đa {MAX_BOOKING_ADVANCE_DAYS} ngày.");
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

            // Kiểm tra point có tồn tại và có status hợp lệ (check charging point existence and status)
            var chargingPoint = await _db.ChargingPoints
                .Include(cp => cp.Station)
                .FirstOrDefaultAsync(p => p.PointId == pointId);

            if (chargingPoint == null)
                throw new KeyNotFoundException("Charging point not found (không tìm thấy điểm sạc).");

            // Kiểm tra status của charging point
            // ✅ Cho phép đặt reservation cho slot tương lai dù point đang "in_use" (do walk-in hoặc reservation session)
            // Walk-in session sẽ tự động dừng trước reservation start time
            // Reservation session hiện tại sẽ kết thúc trước reservation mới bắt đầu
            if (chargingPoint.Status != "available")
            {
                Console.WriteLine($"[ValidateTimeSlotAsync] Point {pointId} status: {chargingPoint.Status}, startUtc: {startUtc:yyyy-MM-dd HH:mm}, now: {now:yyyy-MM-dd HH:mm}, startUtc > now: {startUtc > now}");
                
                // Nếu reservation bắt đầu trong tương lai, cho phép đặt dù point đang "in_use"
                // Vì session hiện tại (walk-in hoặc reservation) sẽ kết thúc trước reservation start time
                if (startUtc > now && chargingPoint.Status == "in_use")
                {
                    // Kiểm tra xem có session đang active trên point này không
                    var hasActiveSession = await _db.ChargingSessions
                        .AnyAsync(s => 
                            s.PointId == pointId 
                            && s.Status == "in_progress");
                    
                    Console.WriteLine($"[ValidateTimeSlotAsync] Point {pointId} has active session: {hasActiveSession}");
                    
                    // ✅ Cho phép đặt reservation cho slot tương lai dù có session đang active hay không
                    // Vì session hiện tại sẽ kết thúc trước reservation start time
                    Console.WriteLine($"[ValidateTimeSlotAsync] Point {pointId} is in_use but reservation starts in future ({startUtc:HH:mm}), allowing booking");
                    // Không throw error, tiếp tục validate
                }
                else if (chargingPoint.Status == "maintenance" || chargingPoint.Status == "offline")
                {
                    // Point đang maintenance hoặc offline → Block tất cả reservation
                    throw new InvalidOperationException($"Charging point is not available (điểm sạc không khả dụng). Current status: {chargingPoint.Status ?? "null"}");
                }
                else
                {
                    // Reservation bắt đầu ngay bây giờ hoặc trong quá khứ, và point status không phải "in_use" hoặc "maintenance"/"offline"
                    // Block reservation
                    Console.WriteLine($"[ValidateTimeSlotAsync] Blocking reservation: startUtc={startUtc:yyyy-MM-dd HH:mm}, now={now:yyyy-MM-dd HH:mm}, status={chargingPoint.Status}");
                    throw new InvalidOperationException($"Charging point is not available (điểm sạc không khả dụng). Current status: {chargingPoint.Status ?? "null"}");
                }
            }

            // Kiểm tra status của station
            if (chargingPoint.Station == null || chargingPoint.Station.Status != "active")
                throw new InvalidOperationException("Charging station is not active (trạm sạc không hoạt động).");

            // Kiểm tra trùng giờ (overlap) trên cùng point với reservation
            // Overlap khi: NOT (newEnd <= start OR newStart >= end)
            // ✅ CHỈ check các status đang active: booked, checked_in, in_progress
            // ✅ KHÔNG check completed, cancelled, no_show vì những slot đó đã trống
            var reservationOverlap = await _db.Reservations
                .Where(r => r.PointId == pointId && (r.Status == "booked" || r.Status == "checked_in" || r.Status == "in_progress"))
                .AnyAsync(r => !(endUtc <= r.StartTime || startUtc >= r.EndTime));

            if (reservationOverlap)
                throw new InvalidOperationException("Time slot not available (khung giờ đã có người đặt).");

            // ✅ Kiểm tra trùng giờ (overlap) với walk-in session đang active
            var walkInSessions = await _db.ChargingSessions
                .Where(s => 
                    s.PointId == pointId 
                    && s.Status == "in_progress"
                    && !s.ReservationId.HasValue // Walk-in session (không có ReservationId)
                    && s.StartTime < endUtc) // Session bắt đầu trước khi slot kết thúc
                .ToListAsync();
            
            var walkInSessionOverlap = false;
            foreach (var session in walkInSessions)
            {
                DateTime? effectiveEndTime = null;
                
                // Nếu session có EndTime, dùng EndTime
                if (session.EndTime.HasValue)
                {
                    effectiveEndTime = session.EndTime.Value;
                }
                // Nếu session chưa có EndTime nhưng có maxEndTime trong Notes, dùng maxEndTime
                else if (!string.IsNullOrEmpty(session.Notes))
                {
                    try
                    {
                        var notesJson = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(session.Notes);
                        if (notesJson.TryGetProperty("maxEndTime", out var maxEndTimeElement))
                        {
                            if (maxEndTimeElement.TryGetDateTime(out var maxEndTime))
                            {
                                effectiveEndTime = maxEndTime;
                            }
                        }
                    }
                    catch
                    {
                        // Ignore parse errors
                    }
                }
                
                // Nếu không có EndTime và không có maxEndTime, cần check overlap cẩn thận
                if (!effectiveEndTime.HasValue)
                {
                    // Option 2: Cho phép đặt reservation và tự động set maxEndTime cho walk-in session
                    // Chỉ block nếu reservation overlap với thời gian hiện tại (now đang trong slot reservation)
                    // Nếu reservation bắt đầu trong tương lai, cho phép đặt và UpdateWalkInSessionsForNewReservationAsync sẽ set maxEndTime
                    
                    if (startUtc > now)
                    {
                        // Reservation bắt đầu trong tương lai
                        // Nếu walk-in session bắt đầu trước reservation start time, không block
                        // UpdateWalkInSessionsForNewReservationAsync sẽ tự động set maxEndTime cho walk-in session
                        // Chỉ block nếu walk-in session bắt đầu trong slot reservation (rất hiếm, nhưng vẫn check)
                        if (session.StartTime >= startUtc && session.StartTime < endUtc)
                        {
                            // Walk-in session bắt đầu trong slot reservation → Block
                            walkInSessionOverlap = true;
                            break;
                        }
                        // Nếu walk-in session bắt đầu trước reservation start time, cho phép đặt
                        // maxEndTime sẽ được set trong UpdateWalkInSessionsForNewReservationAsync
                    }
                    else
                    {
                        // Reservation bắt đầu trong quá khứ hoặc hiện tại
                        // Chỉ block nếu session bắt đầu trong slot này hoặc now đang trong slot này
                        var sessionStartInSlot = session.StartTime >= startUtc && session.StartTime < endUtc;
                        var nowInSlot = session.StartTime <= now && now >= startUtc && now < endUtc;
                        
                        if (sessionStartInSlot || nowInSlot)
                        {
                            walkInSessionOverlap = true;
                            break;
                        }
                    }
                }
                else
                {
                    // Có effectiveEndTime: check overlap bình thường
                    if (effectiveEndTime.Value > startUtc && session.StartTime < endUtc)
                    {
                        walkInSessionOverlap = true;
                        break;
                    }
                }
            }

            if (walkInSessionOverlap)
                throw new InvalidOperationException("Time slot not available (khung giờ đang có khách vãng lai đang sạc).");
        }
    }
}
