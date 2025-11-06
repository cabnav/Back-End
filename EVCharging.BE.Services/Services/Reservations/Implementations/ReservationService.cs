using EVCharging.BE.Common.DTOs.Reservations;
using EVCharging.BE.Common.DTOs.Stations;
using EVCharging.BE.Common.DTOs.Users;
using EVCharging.BE.DAL;
using EVCharging.BE.DAL.Entities;
using EVCharging.BE.Services.Services.Payment;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PaymentEntity = EVCharging.BE.DAL.Entities.Payment;

namespace EVCharging.BE.Services.Services.Reservations.Implementations
{
    /// <summary>
    /// Reservation business logic (nghiệp vụ đặt chỗ)
    /// </summary>
    public class ReservationService : IReservationService
    {
        private readonly EvchargingManagementContext _db;   
        private readonly ITimeValidationService _timeValidator;
        private readonly IWalletService _walletService;

        private const decimal DEPOSIT_AMOUNT = 20000m; // Cọc 20,000 VNĐ

        public ReservationService(
            EvchargingManagementContext db,
            ITimeValidationService timeValidator,
            IWalletService walletService)
        {
            _db = db;
            _timeValidator = timeValidator;
            _walletService = walletService;
        }

        /// <summary>
        /// Create reservation (tạo đặt chỗ)
        /// </summary>
        public async Task<ReservationDTO> CreateReservationAsync(int userId, ReservationRequest request)
        {
            // Map userId -> driverId (ánh xạ người dùng sang tài xế)
            var driverId = await _db.DriverProfiles
                .Where(d => d.UserId == userId)
                .Select(d => d.DriverId)
                .FirstOrDefaultAsync();

            if (driverId == 0)
                throw new InvalidOperationException("Driver profile not found (không tìm thấy hồ sơ tài xế).");

            // Sử dụng logic mới: Date + Hour thay vì StartTime + DurationMinutes
            DateTime startUtc, endUtc;
            
            if (request.LegacyStartTime.HasValue)
            {
                // Tương thích ngược với API cũ
                startUtc = DateTime.SpecifyKind(request.LegacyStartTime.Value, DateTimeKind.Utc);
                endUtc = startUtc.AddMinutes(request.DurationMinutes);
            }
            else
            {
                // Logic mới: sử dụng Date + Hour
                startUtc = DateTime.SpecifyKind(request.StartTime, DateTimeKind.Utc);
                endUtc = DateTime.SpecifyKind(request.EndTime, DateTimeKind.Utc);
            }

            // Validate slot (kiểm tra khung giờ)
            await _timeValidator.ValidateTimeSlotAsync(request.PointId, startUtc, endUtc);

            // Sử dụng EF Core execution strategy thay vì TransactionScope
            var strategy = _db.Database.CreateExecutionStrategy();
            var entity = await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _db.Database.BeginTransactionAsync();
                
                try
                {
                    // Re-check overlap inside transaction (kiểm tra trùng lần nữa trong giao dịch)
                    // ✅ CHỈ check các status đang active: booked, checked_in, in_progress
                    // ✅ KHÔNG check completed, cancelled, no_show vì những slot đó đã trống
                    var overlap = await _db.Reservations
                        .Where(r => r.PointId == request.PointId && (r.Status == "booked" || r.Status == "checked_in" || r.Status == "in_progress"))
                        .AnyAsync(r => !(endUtc <= r.StartTime || startUtc >= r.EndTime));

                    if (overlap)
                        throw new InvalidOperationException("Time slot not available (khung giờ đã có người đặt).");

                    // Tạo mã đặt chỗ (reservation code) phục vụ QR/check-in
                    // Code phải đúng format (CHECK constraint: CK_Reservation_Code_Format):
                    // - Độ dài 8-12 ký tự
                    // - Chỉ chữ cái HOA A-Z và số 2-9 (không có O,I,0,1 để tránh nhầm lẫn)
                    string reservationCode;
                    var random = new Random();
                    var validChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // 32 ký tự hợp lệ

                    // Thử tối đa 5 lần để tránh trùng hoặc sai format
                    for (int attempts = 0; attempts < 5; attempts++)
                    {
                        // Sinh mã 8 ký tự từ bộ ký tự hợp lệ
                        var chars = new char[8];
                        for (int i = 0; i < 8; i++)
                        {
                            chars[i] = validChars[random.Next(validChars.Length)];
                        }
                        reservationCode = new string(chars);

                        // Đảm bảo code chưa tồn tại
                        var exists = await _db.Reservations.AnyAsync(r => r.ReservationCode == reservationCode);
                        if (!exists)
                        {
                            var reservation = new Reservation
                            {
                                DriverId = driverId,
                                PointId = request.PointId,
                                StartTime = startUtc,
                                EndTime = endUtc,
                                Status = "booked",
                                CreatedAt = DateTime.UtcNow,
                                UpdatedAt = DateTime.UtcNow,
                                ReservationCode = reservationCode
                            };

                            _db.Reservations.Add(reservation);
                            await _db.SaveChangesAsync();
                            await transaction.CommitAsync();

                            // ✅ Sau khi tạo reservation thành công, check và update walk-in sessions nếu có overlap
                            await UpdateWalkInSessionsForNewReservationAsync(request.PointId, startUtc);

                            return reservation;
                        }
                    }

                    throw new InvalidOperationException("Could not generate valid reservation code (không thể sinh mã đặt chỗ hợp lệ).");
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });

            // Load navigation to map DTO (nạp quan hệ để ánh xạ DTO)
            await _db.Entry(entity).Reference(r => r.Point).LoadAsync();
            if (entity.Point != null)
                await _db.Entry(entity.Point).Reference(p => p.Station).LoadAsync();  // 🔥 Load station info
            await _db.Entry(entity).Reference(r => r.Driver).LoadAsync();
            if (entity.Driver != null)
                await _db.Entry(entity.Driver).Reference(d => d.User).LoadAsync();

            // Thu cọc 20,000 VNĐ sau khi tạo reservation thành công
            try
            {
                var walletBalance = await _walletService.GetBalanceAsync(userId);
                if (walletBalance >= DEPOSIT_AMOUNT)
                {
                    // Ví đủ tiền: trừ từ ví và tạo Payment record
                    await _walletService.DebitAsync(
                        userId,
                        DEPOSIT_AMOUNT,
                        $"Cọc đặt chỗ #{entity.ReservationCode}",
                        entity.ReservationId
                    );

                    // Tạo Payment record cho deposit
                    // Tạo InvoiceNumber để đảm bảo unique constraint
                    var invoiceNumber = $"DEP{entity.ReservationId}_{DateTime.UtcNow:yyyyMMddHHmmss}";
                    if (invoiceNumber.Length > 50)
                    {
                        invoiceNumber = invoiceNumber.Substring(0, 50);
                    }
                    
                    // Đảm bảo InvoiceNumber không trùng (nếu trùng, thêm suffix)
                    var originalInvoiceNumber = invoiceNumber;
                    int suffix = 0;
                    while (await _db.Payments.AnyAsync(p => p.InvoiceNumber == invoiceNumber))
                    {
                        suffix++;
                        var suffixStr = suffix.ToString();
                        invoiceNumber = originalInvoiceNumber.Length + suffixStr.Length <= 50
                            ? originalInvoiceNumber + suffixStr
                            : originalInvoiceNumber.Substring(0, 50 - suffixStr.Length) + suffixStr;
                    }
                    
                    var depositPayment = new PaymentEntity
                    {
                        UserId = userId,
                        ReservationId = entity.ReservationId,
                        Amount = DEPOSIT_AMOUNT,
                        PaymentMethod = "wallet",
                        PaymentStatus = "success",
                        PaymentType = "deposit", // ⭐ Quan trọng: Phân biệt loại payment
                        InvoiceNumber = invoiceNumber,
                        CreatedAt = DateTime.UtcNow
                    };

                    _db.Payments.Add(depositPayment);
                    await _db.SaveChangesAsync();
                }
                else
                {
                    // Ví không đủ: throw exception với message đặc biệt để controller nhận biết
                    throw new InvalidOperationException(
                        $"WALLET_INSUFFICIENT|Ví không đủ tiền để cọc. " +
                        $"Số dư hiện tại: {walletBalance:F0} VNĐ, " +
                        $"Cần: {DEPOSIT_AMOUNT:F0} VNĐ. " +
                        $"vui lòng nạp thêm tiền vào ví hoặc thanh toán cọc qua Momo.|{entity.ReservationId}"
                    );
                }
            }
            catch (InvalidOperationException)
            {
                // Re-throw để controller xử lý
                throw;
            }
            catch (Exception ex)
            {
                // Nếu có lỗi khi save payment sau khi đã trừ tiền, đây là lỗi nghiêm trọng
                // Log chi tiết và throw lại để hệ thống biết có vấn đề
                Console.WriteLine($"❌ [CreateReservationAsync] CRITICAL ERROR: Tiền đã trừ nhưng không thể tạo payment record!");
                Console.WriteLine($"   Error: {ex.Message}");
                Console.WriteLine($"   StackTrace: {ex.StackTrace}");
                throw new InvalidOperationException(
                    $"Đã trừ tiền cọc nhưng không thể lưu payment record. Vui lòng liên hệ quản trị viên. Lỗi: {ex.Message}",
                    ex
                );
            }

            // Gửi thông báo cho người dùng (English)
            try
            {
                var userIdForNotification = entity.Driver?.User?.UserId;
                if (userIdForNotification.HasValue)
                {
                    var startIso = entity.StartTime.ToString("o");
                    var stationName = entity.Point?.Station?.Name ?? "charging station";
                    var title = "Reservation confirmed";
                    var message = $"Your charging reservation is confirmed at {stationName}. Please arrive on time. The latest allowed check-in is within 30 minutes from the start time ({startIso} UTC).";

                    _db.Notifications.Add(new EVCharging.BE.DAL.Entities.Notification
                    {
                        UserId = userIdForNotification.Value,
                        Title = title,
                        Message = message,
                        Type = "reservation_created",
                        IsRead = false,
                        CreatedAt = DateTime.UtcNow
                    });

                    await _db.SaveChangesAsync();
                }
            }
            catch
            {
                // best effort; không chặn flow nếu thông báo lỗi
            }

            return MapToDto(entity);
        }

        /// <summary>
        /// Get reservations by filter (lấy danh sách đặt chỗ theo tiêu chí)
        /// </summary>
        public async Task<IEnumerable<ReservationDTO>> GetReservationsAsync(ReservationFilter filter)
        {
            var q = _db.Reservations
                .Include(r => r.Point)
                    .ThenInclude(p => p.Station)  // 🔥 Include station cho UX
                .Include(r => r.Driver).ThenInclude(d => d.User)
                .AsQueryable();

            if (filter.DriverId.HasValue)
                q = q.Where(r => r.DriverId == filter.DriverId.Value);

            if (filter.PointId.HasValue)
                q = q.Where(r => r.PointId == filter.PointId.Value);

            if (filter.StationId.HasValue)
                q = q.Where(r => r.Point.StationId == filter.StationId.Value);

            if (!string.IsNullOrWhiteSpace(filter.Status))
                q = q.Where(r => r.Status == filter.Status);

            if (filter.FromDate.HasValue)
            {
                var fromUtc = DateTime.SpecifyKind(filter.FromDate.Value, DateTimeKind.Utc);
                q = q.Where(r => r.StartTime >= fromUtc);
            }

            if (filter.ToDate.HasValue)
            {
                var toUtc = DateTime.SpecifyKind(filter.ToDate.Value, DateTimeKind.Utc);
                q = q.Where(r => r.EndTime <= toUtc);
            }

            // Phân trang (pagination)
            var skip = filter.Page <= 1 ? 0 : (filter.Page - 1) * filter.PageSize;
            q = q.OrderByDescending(r => r.StartTime).Skip(skip).Take(filter.PageSize);

            var list = await q.ToListAsync();
            return list.Select(MapToDto);
        }

        /// <summary>
        /// Get upcoming reservations for a user (các đặt chỗ sắp tới của người dùng)
        /// </summary>
        public async Task<IEnumerable<ReservationDTO>> GetUpcomingReservationsAsync(int userId, TimeSpan horizon)
        {
            var driverId = await _db.DriverProfiles
                .Where(d => d.UserId == userId)
                .Select(d => d.DriverId)
                .FirstOrDefaultAsync();

            if (driverId == 0) return Enumerable.Empty<ReservationDTO>();

            // Dùng UTC để nhất quán toàn hệ thống
            var now = DateTime.UtcNow;
            var to = now.Add(horizon);

            var list = await _db.Reservations
                .Include(r => r.Point)
                    .ThenInclude(p => p.Station)  // 🔥 Include station cho UX
                .Include(r => r.Driver).ThenInclude(d => d.User)
                .Where(r => r.DriverId == driverId
                         && r.Status == "booked"
                         && r.StartTime >= now
                         && r.StartTime <= to)
                .OrderBy(r => r.StartTime)
                .ToListAsync();

            return list.Select(MapToDto);
        }

        /// <summary>
        /// Cancel reservation (huỷ đặt chỗ)
        /// </summary>
        public async Task<bool> CancelReservationAsync(int userId, int reservationId, string? reason = null)
        {
            var driverId = await _db.DriverProfiles
                .Where(d => d.UserId == userId)
                .Select(d => d.DriverId)
                .FirstOrDefaultAsync();

            if (driverId == 0)
                throw new InvalidOperationException("Driver profile not found (không tìm thấy hồ sơ tài xế).");

            var entity = await _db.Reservations.FirstOrDefaultAsync(r => r.ReservationId == reservationId);
            if (entity == null) return false;

            if (entity.DriverId != driverId)
                throw new UnauthorizedAccessException("Not owner (không phải chủ sở hữu đặt chỗ).");

            if (entity.Status is "cancelled" or "completed" or "no_show")
                throw new InvalidOperationException("Cannot cancel (không thể huỷ ở trạng thái hiện tại).");

            // Xử lý hoàn cọc nếu có deposit payment thành công
            await ProcessDepositRefundAsync(userId, entity);

            entity.Status = "cancelled";
            entity.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return true;
        }

        /// <summary>
        /// Cancel reservation by code (huỷ đặt chỗ bằng mã)
        /// </summary>
        public async Task<bool> CancelReservationByCodeAsync(int userId, string reservationCode, string? reason = null)
        {
            var driverId = await _db.DriverProfiles
                .Where(d => d.UserId == userId)
                .Select(d => d.DriverId)
                .FirstOrDefaultAsync();

            if (driverId == 0)
                throw new InvalidOperationException("Driver profile not found (không tìm thấy hồ sơ tài xế).");

            var entity = await _db.Reservations
                .FirstOrDefaultAsync(r => r.ReservationCode == reservationCode && r.DriverId == driverId);
            
            if (entity == null) return false;

            if (entity.Status is "cancelled" or "completed" or "no_show")
                throw new InvalidOperationException("Cannot cancel (không thể huỷ ở trạng thái hiện tại).");

            // Xử lý hoàn cọc nếu có deposit payment thành công
            await ProcessDepositRefundAsync(userId, entity);

            entity.Status = "cancelled";
            entity.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return true;
        }

        /// <summary>
        /// Xử lý hoàn cọc khi hủy reservation
        /// - Nếu hủy trước 30 phút trước start time → hoàn cọc
        /// - Nếu hủy dưới 30 phút trước start time → không hoàn cọc
        /// </summary>
        private async Task ProcessDepositRefundAsync(int userId, DAL.Entities.Reservation reservation)
        {
            try
            {
                // Kiểm tra xem có deposit payment thành công không
                var depositPayment = await _db.Payments
                    .Where(p => p.ReservationId == reservation.ReservationId &&
                               p.PaymentStatus == "success" &&
                               p.PaymentType == "deposit" &&
                               p.Amount == DEPOSIT_AMOUNT)
                    .OrderByDescending(p => p.CreatedAt)
                    .FirstOrDefaultAsync();

                if (depositPayment == null)
                {
                    // Không có deposit payment → không cần hoàn cọc
                    return;
                }

                // Kiểm tra thời gian hủy: nếu hủy trước 30 phút trước start time → hoàn cọc
                var now = DateTime.UtcNow;
                var cancelBeforeStart = reservation.StartTime - now;
                const int REFUND_DEADLINE_MINUTES = 30; // 30 phút

                if (cancelBeforeStart.TotalMinutes >= REFUND_DEADLINE_MINUTES)
                {
                    // Hủy trước 30 phút → hoàn cọc vào ví
                    await _walletService.CreditAsync(
                        userId,
                        DEPOSIT_AMOUNT,
                        $"Hoàn cọc hủy đặt chỗ #{reservation.ReservationCode}",
                        reservation.ReservationId
                    );

                    Console.WriteLine($"✅ Đã hoàn cọc {DEPOSIT_AMOUNT:F0} VNĐ cho reservation {reservation.ReservationId} (hủy trước {cancelBeforeStart.TotalMinutes:F0} phút)");
                }
                else
                {
                    // Hủy dưới 30 phút → không hoàn cọc
                    Console.WriteLine($"⚠️ Không hoàn cọc cho reservation {reservation.ReservationId} (hủy chỉ còn {cancelBeforeStart.TotalMinutes:F0} phút, yêu cầu tối thiểu {REFUND_DEADLINE_MINUTES} phút)");
                }
            }
            catch (Exception ex)
            {
                // Log lỗi nhưng không throw để reservation vẫn được hủy
                Console.WriteLine($"❌ Lỗi khi xử lý hoàn cọc cho reservation {reservation.ReservationId}: {ex.Message}");
                Console.WriteLine($"   StackTrace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Get reservation by code for specific user (tra cứu đặt chỗ bằng mã)
        /// </summary>
        public async Task<ReservationDTO?> GetReservationByCodeAsync(int userId, string reservationCode)
        {
            // Lấy driverId từ userId
            var driverId = await _db.DriverProfiles
                .Where(d => d.UserId == userId)
                .Select(d => d.DriverId)
                .FirstOrDefaultAsync();

            if (driverId == 0)
                return null;

            // Tìm reservation với code và driver ID
            var reservation = await _db.Reservations
                .Include(r => r.Point)
                    .ThenInclude(p => p.Station)
                .Include(r => r.Driver)
                    .ThenInclude(d => d.User)
                .FirstOrDefaultAsync(r => r.ReservationCode == reservationCode && r.DriverId == driverId);

            if (reservation == null)
                return null;

            return MapToDto(reservation);
        }

        /// <summary>
        /// Đánh dấu reservation đã check-in (driver sở hữu)
        /// </summary>
        public async Task<bool> MarkCheckedInAsync(int userId, string reservationCode)
        {
            var driverId = await _db.DriverProfiles
                .Where(d => d.UserId == userId)
                .Select(d => d.DriverId)
                .FirstOrDefaultAsync();

            if (driverId == 0)
                return false;

            var entity = await _db.Reservations
                .FirstOrDefaultAsync(r => r.ReservationCode == reservationCode && r.DriverId == driverId);

            if (entity == null)
                return false;

            // ✅ Set status = "checked_in" khi đã check-in và bắt đầu session
            // Lưu ý: CHECK constraint trong database phải cho phép giá trị "checked_in"
            entity.Status = "checked_in";
            entity.UpdatedAt = DateTime.UtcNow;
            
            try
            {
                await _db.SaveChangesAsync();
                return true;
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex) when (ex.InnerException?.Message?.Contains("CHECK constraint") == true)
            {
                // ✅ Fallback: Nếu constraint không cho phép "checked_in", dùng "in_progress"
                Console.WriteLine($"⚠️ [MarkCheckedInAsync] CHECK constraint error. Trying with 'in_progress' status instead.");
                entity.Status = "in_progress";
                await _db.SaveChangesAsync();
                return true;
            }
        }

        // -----------------------
        // Mapping helpers (hàm ánh xạ)
        // -----------------------
        private static ReservationDTO MapToDto(Reservation r)
        {
            return new ReservationDTO
            {
                ReservationId = r.ReservationId,
                DriverId = r.DriverId,
                PointId = r.PointId,
                StartTime = r.StartTime,
                EndTime = r.EndTime,
                Status = r.Status ?? "booked",
                CreatedAt = r.CreatedAt ?? DateTime.UtcNow,
                UpdatedAt = r.UpdatedAt ?? DateTime.UtcNow,
                ReservationCode = r.ReservationCode, // từ partial class
                ChargingPoint = r.Point is null ? null : new ChargingPointDTO
                {
                    PointId = r.Point.PointId,
                    StationId = r.Point.StationId,
                    ConnectorType = r.Point.ConnectorType,
                    PowerOutput = r.Point.PowerOutput ?? 0,
                    PricePerKwh = r.Point.PricePerKwh,
                    Status = r.Point.Status,
                    QrCode = r.Point.QrCode ?? "",
                    CurrentPower = r.Point.CurrentPower,      
                    LastMaintenance = r.Point.LastMaintenance,  
                    // 🔥 UX improvement: Thông tin trạm hữu ích cho người dùng  
                    StationName = r.Point.Station?.Name,
                    StationAddress = r.Point.Station?.Address
                },
                Driver = r.Driver?.User is null ? null : new UserDTO
                {
                    UserId = r.Driver.User.UserId,
                    Name = r.Driver.User.Name,
                    Email = r.Driver.User.Email,
                    Phone = r.Driver.User.Phone
                }
            };
        }

        /// <summary>
        /// Cập nhật walk-in sessions khi có reservation mới được tạo (update maxEndTime trong Notes)
        /// </summary>
        private async Task UpdateWalkInSessionsForNewReservationAsync(int pointId, DateTime reservationStartTime)
        {
            try
            {
                var now = DateTime.UtcNow;
                var bufferMinutes = 5; // Buffer 5 phút trước khi reservation bắt đầu
                var maxEndTime = reservationStartTime.AddMinutes(-bufferMinutes);

                // Tìm tất cả walk-in sessions đang active trên point này (không có ReservationId)
                var walkInSessions = await _db.ChargingSessions
                    .Where(s => 
                        s.PointId == pointId 
                        && s.Status == "in_progress"
                        && !s.ReservationId.HasValue
                        && s.StartTime < reservationStartTime) // Session đã bắt đầu trước reservation
                    .ToListAsync();

                foreach (var session in walkInSessions)
                {
                    try
                    {
                        // Parse Notes hiện tại để lấy maxEndTime cũ (nếu có)
                        DateTime? currentMaxEndTime = null;
                        if (!string.IsNullOrEmpty(session.Notes))
                        {
                            try
                            {
                                var notesJson = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(session.Notes);
                                if (notesJson.TryGetProperty("maxEndTime", out var maxEndTimeElement))
                                {
                                    if (maxEndTimeElement.ValueKind == System.Text.Json.JsonValueKind.String)
                                    {
                                        if (DateTime.TryParse(maxEndTimeElement.GetString(), out var parsedMaxEndTime))
                                        {
                                            currentMaxEndTime = parsedMaxEndTime;
                                        }
                                    }
                                    else if (maxEndTimeElement.ValueKind == System.Text.Json.JsonValueKind.Number)
                                    {
                                        // Nếu là timestamp (số), convert sang DateTime
                                        var timestamp = maxEndTimeElement.GetInt64();
                                        currentMaxEndTime = DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime;
                                    }
                                }
                            }
                            catch
                            {
                                // Notes không phải JSON hợp lệ, bỏ qua
                            }
                        }

                        // Chỉ update nếu maxEndTime mới sớm hơn maxEndTime cũ (hoặc chưa có maxEndTime)
                        if (!currentMaxEndTime.HasValue || maxEndTime < currentMaxEndTime.Value)
                        {
                            // Update hoặc tạo mới Notes với maxEndTime mới
                            // ✅ Serialize với ISO 8601 format để đảm bảo parse được
                            var options = new System.Text.Json.JsonSerializerOptions
                            {
                                WriteIndented = false
                            };
                            var maxEndTimeInfo = new
                            {
                                maxEndTime = maxEndTime.ToString("O"), // ISO 8601 format
                                reason = "upcoming_reservation",
                                autoStop = true,
                                reservationStartTime = reservationStartTime.ToString("O") // ISO 8601 format
                            };
                            session.Notes = System.Text.Json.JsonSerializer.Serialize(maxEndTimeInfo, options);
                            Console.WriteLine($"[UpdateWalkInSessionsForNewReservation] Updated walk-in session SessionId={session.SessionId} with maxEndTime={maxEndTime} (reservation starts at {reservationStartTime})");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️ [UpdateWalkInSessionsForNewReservation] Error updating session SessionId={session.SessionId}: {ex.Message}");
                    }
                }

                // Save changes
                if (walkInSessions.Count > 0)
                {
                    await _db.SaveChangesAsync();
                    Console.WriteLine($"[UpdateWalkInSessionsForNewReservation] Updated {walkInSessions.Count} walk-in session(s) for new reservation at {reservationStartTime}");
                }
            }
            catch (Exception ex)
            {
                // Log error nhưng không throw để không ảnh hưởng đến việc tạo reservation
                Console.WriteLine($"⚠️ [UpdateWalkInSessionsForNewReservation] Error updating walk-in sessions: {ex.Message}");
            }
        }
    }
}
