using EVCharging.BE.Common.DTOs.Reservations;
using EVCharging.BE.Common.DTOs.Stations;
using EVCharging.BE.Common.DTOs.Users;
using EVCharging.BE.DAL;
using EVCharging.BE.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EVCharging.BE.Services.Services.Reservations.Implementations
{
    /// <summary>
    /// Reservation business logic (nghiệp vụ đặt chỗ)
    /// </summary>
    public class ReservationService : IReservationService
    {
        private readonly EvchargingManagementContext _db;   
        private readonly ITimeValidationService _timeValidator;

        public ReservationService(
            EvchargingManagementContext db,
            ITimeValidationService timeValidator)
        {
            _db = db;
            _timeValidator = timeValidator;
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
                    var overlap = await _db.Reservations
                        .Where(r => r.PointId == request.PointId && (r.Status == "booked" || r.Status == "completed"))
                        .AnyAsync(r => !(endUtc <= r.StartTime || startUtc >= r.EndTime));

                    if (overlap)
                        throw new InvalidOperationException("Time slot not available (khung giờ đã có người đặt).");

                    // Tạo mã đặt chỗ (reservation code) phục vụ QR/check-in
                    // Code phải đúng format (CHECK constraint: CK_Reservation_Code_Format)
                    // Thử format khác: chỉ số (ví dụ: 12345678)
                    string reservationCode;
                    var random = new Random();

                    // Thử tối đa 5 lần để tránh trùng hoặc sai format
                    for (int attempts = 0; attempts < 5; attempts++)
                    {
                        // Format: chỉ số 8 chữ số
                        reservationCode = random.Next(10000000, 99999999).ToString();

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
            await _db.Entry(entity).Reference(r => r.Driver).LoadAsync();
            if (entity.Driver != null)
                await _db.Entry(entity.Driver).Reference(d => d.User).LoadAsync();

            return MapToDto(entity);
        }

        /// <summary>
        /// Get reservations by filter (lấy danh sách đặt chỗ theo tiêu chí)
        /// </summary>
        public async Task<IEnumerable<ReservationDTO>> GetReservationsAsync(ReservationFilter filter)
        {
            var q = _db.Reservations
                .Include(r => r.Point)
                .Include(r => r.Driver).ThenInclude(d => d.User)
                .AsQueryable();

            if (filter.DriverId.HasValue)
                q = q.Where(r => r.DriverId == filter.DriverId.Value);

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

            var now = DateTime.UtcNow;
            var to = now.Add(horizon);

            var list = await _db.Reservations
                .Include(r => r.Point)
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

            // Optional policy: không cho huỷ nếu sắp bắt đầu trong X phút
            // if (entity.StartTime <= DateTime.UtcNow.AddMinutes(10)) { ... }

            entity.Status = "cancelled";
            entity.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return true;
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
                    // Map tối thiểu (tuỳ DTO của bạn có gì thì map thêm)
                    PointId = r.Point.PointId,
                    StationId = r.Point.StationId,
                    ConnectorType = r.Point.ConnectorType,
                    PricePerKwh = r.Point.PricePerKwh,
                    Status = r.Point.Status
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
    }
}
