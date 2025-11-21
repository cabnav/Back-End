using EVCharging.BE.Common.DTOs.Reservations;
using EVCharging.BE.Common.DTOs.Charging;
using EVCharging.BE.Services.Services.Admin;
using EVCharging.BE.Services.Services.Background;
using EVCharging.BE.Services.Services.Reservations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using EVCharging.BE.DAL;
using Microsoft.EntityFrameworkCore;

namespace EVCharging.BE.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // yêu cầu đăng nhập bằng JWT
    public class ReservationsController : ControllerBase
    {
        private readonly IReservationService _reservationService;
        private readonly EVCharging.BE.Services.Services.Charging.IChargingService _chargingService;
        private readonly IQRCodeService _qrCodeService;
        private readonly IStationSearchService _stationSearchService;
        private readonly ReservationBackgroundOptions _opt;
        private readonly EvchargingManagementContext _db;
        private readonly IDepositService _depositService;

        public ReservationsController(
            IReservationService reservationService,
            IQRCodeService qrCodeService,
            IStationSearchService stationSearchService,
            EVCharging.BE.Services.Services.Charging.IChargingService chargingService,
            IOptions<ReservationBackgroundOptions> opt,
            EvchargingManagementContext db,
            IDepositService depositService)
        {
            _reservationService = reservationService;
            _qrCodeService = qrCodeService;
            _stationSearchService = stationSearchService;
            _chargingService = chargingService;
            _opt = opt.Value;
            _db = db;
            _depositService = depositService;
        }

        // -------------------------------
        // 1️⃣ SEARCH compatible stations (tìm trạm sạc phù hợp)
        // -------------------------------
        [HttpPost("search-stations")]
        public async Task<IActionResult> SearchStations([FromBody] StationSearchRequest request)
        {
            var stations = await _stationSearchService.SearchCompatibleStationsAsync(request);
            return Ok(stations);
        }

        // -------------------------------
        // 5️⃣ CHECK-IN bằng reservation code (start session với StartTime = Reservation.StartTime)
        // -------------------------------
        [HttpPost("{reservationCode}/check-in")]
        public async Task<IActionResult> CheckIn(string reservationCode, [FromBody] ReservationCheckInRequest request)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            if (!ModelState.IsValid)
            {
                return BadRequest(new { message = "Invalid request data", errors = ModelState.Values.SelectMany(v => v.Errors) });
            }

            // 1) Lấy reservation theo code (của chính user)
            var reservation = await _reservationService.GetReservationByCodeAsync(userId, reservationCode);
            if (reservation == null)
                return NotFound(new { message = "Reservation not found or no permission" });

            // ✅ Kiểm tra status của reservation - không cho check-in nếu đã bị hủy, hoàn thành, hoặc no-show
            if (reservation.Status is "cancelled" or "completed" or "no_show")
            {
                return BadRequest(new { 
                    message = $"Cannot check in. This reservation has been {reservation.Status}. " +
                              $"Please create a new reservation or contact support if you believe this is an error." 
                });
            }

            // 2) Kiểm tra thời gian check-in hợp lệ
            var now = DateTime.UtcNow;
            
            // 2.1) Kiểm tra check-in sớm: Cho phép check-in sớm 15 phút NẾU slot trước đó không có ai đặt
            const int EARLY_CHECK_IN_MINUTES = 15;
            var earliestCheckInTime = reservation.StartTime.AddMinutes(-EARLY_CHECK_IN_MINUTES);
            
            if (now < earliestCheckInTime)
            {
                // Quá sớm, không cho check-in
                var minutesUntilEarliest = (int)(earliestCheckInTime - now).TotalMinutes;
                return BadRequest(new { 
                    message = $"Cannot check in too early. " +
                              $"Earliest check-in time: {earliestCheckInTime:yyyy-MM-dd HH:mm} UTC (15 minutes before start), " +
                              $"Reservation starts at: {reservation.StartTime:yyyy-MM-dd HH:mm} UTC, " +
                              $"which is {minutesUntilEarliest} minutes from now."
                });
            }
            
            // Kiểm tra xem có reservation nào đặt slot trước đó không (EndTime = StartTime của reservation hiện tại)
            var previousReservation = await _db.Reservations
                .Where(r => r.PointId == reservation.PointId
                    && r.EndTime == reservation.StartTime
                    && (r.Status == "booked" || r.Status == "checked_in" || r.Status == "in_progress"))
                .FirstOrDefaultAsync();
            
            if (previousReservation != null)
            {
                // Có người đặt slot trước đó → Không cho check-in sớm, chỉ cho từ StartTime
                if (now < reservation.StartTime)
                {
                    var minutesUntilStart = (int)(reservation.StartTime - now).TotalMinutes;
                    return BadRequest(new { 
                        message = $"Cannot check in early. There is a previous reservation ending at {reservation.StartTime:yyyy-MM-dd HH:mm} UTC. " +
                                  $"You can only check in from {reservation.StartTime:yyyy-MM-dd HH:mm} UTC onwards, " +
                                  $"which is {minutesUntilStart} minutes from now."
                    });
                }
            }
            // Nếu không có reservation trước đó → Cho phép check-in sớm 15 phút (đã check ở trên)

            // 2.2) Không cho check-in sau khi đã hết thời gian slot (EndTime)
            if (now > reservation.EndTime)
            {
                return BadRequest(new { 
                    message = $"Cannot check in. This reservation has expired. " +
                              $"Reservation end time: {reservation.EndTime:yyyy-MM-dd HH:mm} UTC, " +
                              $"Current time: {now:yyyy-MM-dd HH:mm} UTC. " +
                              $"The reservation will be automatically cancelled. Your deposit will not be refunded."
                });
            }
            
            // ✅ Cho phép check-in:
            // - Sớm 15 phút NẾU slot trước đó không có ai đặt (earliestCheckInTime <= now < StartTime)
            // - Đúng giờ hoặc muộn trong slot (StartTime <= now <= EndTime)

            // 3) ✅ Tìm charging point từ pointQrCode và validate khớp với reservation
            var chargingPoint = await _db.ChargingPoints
                .FirstOrDefaultAsync(p => p.QrCode == request.PointQrCode);
            
            if (chargingPoint == null)
            {
                return NotFound(new { message = $"Charging point with QR code '{request.PointQrCode}' not found." });
            }

            // ✅ Validate: Point từ QR code phải khớp với PointId trong reservation
            if (chargingPoint.PointId != reservation.PointId)
            {
                return BadRequest(new { 
                    message = $"The QR code '{request.PointQrCode}' does not match your reservation. " +
                              $"Your reservation is for PointId {reservation.PointId}, but the QR code belongs to PointId {chargingPoint.PointId}."
                });
            }

            // 4) Nếu check-in sớm: StartAtUtc = StartTime của reservation
            // Nếu check-in muộn (nhưng trong 30 phút): StartAtUtc = thời điểm check-in hiện tại
            var startAtUtc = now < reservation.StartTime ? reservation.StartTime : now;

            // 5) Bắt đầu phiên sạc với StartAtUtc = StartTime của reservation
            var startReq = new EVCharging.BE.Common.DTOs.Charging.ChargingSessionStartRequest
            {
                ChargingPointId = reservation.PointId,
                DriverId = reservation.DriverId,
                InitialSOC = request.InitialSOC,
                PointQrCode = request.PointQrCode, // ✅ Thêm pointQrCode
                QrCode = request.PointQrCode, // ✅ Set QrCode cho backward compatibility
                StartAtUtc = startAtUtc,
                ReservationCode = reservationCode
            };

            // ✅ QUAN TRỌNG: Force stop walk-in session đang active tại point này (nếu có)
            // Nếu có walk-in session đang active, phải dừng nó trước khi check-in reservation
            var activeWalkInSession = await _db.ChargingSessions
                .Where(s => s.PointId == reservation.PointId
                    && s.Status == "in_progress"
                    && !s.ReservationId.HasValue // Walk-in session (không có ReservationId)
                    && !s.EndTime.HasValue) // Chưa kết thúc
                .FirstOrDefaultAsync();
            
            if (activeWalkInSession != null)
            {
                Console.WriteLine($"[CheckIn] Found active walk-in session {activeWalkInSession.SessionId} at point {reservation.PointId}. Stopping it before check-in.");
                
                // Force stop walk-in session
                // ✅ FinalSoc là int? (nullable), InitialSoc là int (non-nullable), nên chỉ cần FinalSoc ?? InitialSoc
                var stopRequest = new ChargingSessionStopRequest
                {
                    SessionId = activeWalkInSession.SessionId,
                    FinalSOC = activeWalkInSession.FinalSoc ?? activeWalkInSession.InitialSoc
                };
                
                var stoppedSession = await _chargingService.StopSessionAsync(stopRequest);
                if (stoppedSession != null)
                {
                    Console.WriteLine($"[CheckIn] Successfully stopped walk-in session {activeWalkInSession.SessionId} before check-in.");
                }
                else
                {
                    Console.WriteLine($"⚠️ [CheckIn] Failed to stop walk-in session {activeWalkInSession.SessionId}. Proceeding with check-in anyway.");
                }
            }

            // 3) Kiểm tra có thể start session không (method này đã kiểm tra point available + driver không có session active)
            // Truyền startAtUtc để cho phép check-in sớm nếu session đang active sẽ kết thúc trước start time
            var canStart = await _chargingService.CanStartSessionAsync(reservation.PointId, reservation.DriverId, startAtUtc);
            if (!canStart)
            {
                // Kiểm tra chi tiết để có thông báo lỗi rõ ràng hơn
                var pointIsAvailable = await _chargingService.ValidateChargingPointAsync(reservation.PointId, startAtUtc);
                
                // Đợi ValidateChargingPointAsync hoàn thành trước khi query tiếp
                var activeSessions = await _chargingService.GetSessionsByDriverAsync(reservation.DriverId);
                var hasActiveDriverSession = activeSessions.Any(s => s.Status == "in_progress");
                
                string errorMessage;
                if (hasActiveDriverSession)
                {
                    errorMessage = "You already have an active charging session. Please complete it first.";
                }
                else if (!pointIsAvailable)
                {
                    // Point đang in_use bởi session trước hoặc đang maintenance
                    errorMessage = "The charging point is currently in use by the previous time slot session. Please wait until it becomes available.";
                }
                else
                {
                    errorMessage = "Cannot start charging session. The charging point may be unavailable or under maintenance.";
                }
                
                return BadRequest(new { 
                    message = errorMessage,
                    pointId = reservation.PointId,
                    driverId = reservation.DriverId,
                    pointAvailable = pointIsAvailable,
                    hasActiveDriverSession = hasActiveDriverSession
                });
            }

            // 4) Bắt đầu phiên sạc
            var session = await _chargingService.StartSessionAsync(startReq);
            if (session == null)
            {
                // Kiểm tra chi tiết tại sao StartSessionAsync fail (re-check sau khi StartSessionAsync fail)
                var pointIsAvailableAfter = await _chargingService.ValidateChargingPointAsync(reservation.PointId, startAtUtc);
                var driverIsValid = await _chargingService.ValidateDriverAsync(reservation.DriverId);
                var canStartAfterCheck = await _chargingService.CanStartSessionAsync(reservation.PointId, reservation.DriverId, startAtUtc);
                var activeSessions = await _chargingService.GetSessionsByDriverAsync(reservation.DriverId);
                var hasActiveDriverSession = activeSessions.Any(s => s.Status == "in_progress");
                
                string errorMessage;
                if (!driverIsValid)
                {
                    errorMessage = "Driver profile not found or invalid. Please contact support.";
                }
                else if (hasActiveDriverSession)
                {
                    errorMessage = "You already have an active charging session. Please complete it first.";
                }
                else if (!pointIsAvailableAfter)
                {
                    // Point có thể đang bị chiếm bởi session trước (slot trước)
                    errorMessage = "The charging point is currently in use by another session. Please wait until the previous time slot session completes.";
                }
                else if (!canStartAfterCheck)
                {
                    errorMessage = "Cannot start charging session. The charging point may be busy or you have restrictions.";
                }
                else
                {
                    errorMessage = "Failed to start charging session. Please try again or contact support.";
                }
                
                return BadRequest(new { 
                    message = errorMessage,
                    pointId = reservation.PointId,
                    driverId = reservation.DriverId,
                    pointAvailable = pointIsAvailableAfter,
                    driverValid = driverIsValid,
                    canStart = canStartAfterCheck,
                    hasActiveDriverSession = hasActiveDriverSession,
                    checkInTime = now,
                    reservationStartTime = reservation.StartTime
                });
            }

            // 4) Đánh dấu reservation đã check-in để tránh worker auto-cancel
            await _reservationService.MarkCheckedInAsync(userId, reservationCode);

            return Ok(new { message = "Checked-in successfully.", data = session });
        }

        // -------------------------------
        // 2️⃣ GET compatible points at station (lấy điểm sạc phù hợp tại trạm)
        // -------------------------------
        [HttpGet("stations/{stationId}/compatible-points")]
        public async Task<IActionResult> GetCompatiblePoints(int stationId, [FromQuery] string connectorType)
        {
            var points = await _stationSearchService.GetCompatiblePointsAsync(stationId, connectorType);
            return Ok(points);
        }

        // -------------------------------
        // 3️⃣ GET available time slots (lấy khung giờ có sẵn)
        // -------------------------------
        [HttpGet("points/{pointId}/time-slots")]
        public async Task<IActionResult> GetTimeSlots(int pointId, [FromQuery] DateTime date)
        {
            var timeSlots = await _stationSearchService.GetAvailableTimeSlotsAsync(pointId, date);
            return Ok(timeSlots);
        }

        // -------------------------------
        // 4️⃣ CREATE reservation (tạo đặt chỗ)
        // -------------------------------
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ReservationRequest request)
        {
            try
            {
                // Lấy userId từ JWT claims
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

                var result = await _reservationService.CreateReservationAsync(userId, request);
                return CreatedAtAction(nameof(GetReservationByCode), new { reservationCode = result.ReservationCode }, result);
            }
            catch (InvalidOperationException ex)
            {
                // Kiểm tra xem có phải lỗi ví không đủ không
                if (ex.Message.Contains("WALLET_INSUFFICIENT"))
                {
                    // Parse message để lấy reservationId
                    var parts = ex.Message.Split('|');
                    var reservationId = parts.Length > 2 ? int.Parse(parts[2]) : 0;
                    var userMessage = parts.Length > 1 ? parts[1] : ex.Message;

                    // Lấy reservationCode từ database để trả về cho người dùng
                    // Người dùng cần mã này để thanh toán qua MoMo
                    string? reservationCode = null;
                    if (reservationId > 0)
                    {
                        try
                        {
                            var reservation = await _db.Reservations
                                .Where(r => r.ReservationId == reservationId)
                                .Select(r => new { r.ReservationCode })
                                .FirstOrDefaultAsync();
                            
                            if (reservation != null)
                            {
                                reservationCode = reservation.ReservationCode;
                            }
                        }
                        catch
                        {
                            // Nếu không lấy được reservationCode, vẫn trả về response
                            // Người dùng có thể dùng reservationId hoặc gọi API khác để lấy code
                        }
                    }

                    // Lấy giá tiền cọc hiện tại
                    var depositAmount = await _depositService.GetCurrentDepositAmountAsync();

                    // Trả về response yêu cầu thanh toán cọc qua MoMo
                    return BadRequest(new
                    {
                        message = userMessage,
                        requiresDepositPayment = true,
                        depositAmount = depositAmount,
                        reservationId = reservationId,
                        reservationCode = reservationCode, // ✅ Thêm reservationCode để người dùng dùng ngay
                        paymentMethod = "momo",
                        action = "Vui lòng gọi API /api/payments/reservation-deposit-momo để thanh toán cọc"
                    });
                }

                // Catch validation errors (time slot closed, booking cutoff, etc.)
                return BadRequest(new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                // Catch invalid arguments (invalid time range, etc.)
                return BadRequest(new { message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                // Catch not found errors (point not found, etc.)
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                // Catch other unexpected errors
                return StatusCode(500, new { message = "An error occurred while creating the reservation.", error = ex.Message });
            }
        }


        // -------------------------------
        // 🔍 GET reservation by code (tra cứu đặt chỗ bằng mã)
        // -------------------------------
        [HttpGet("lookup/{reservationCode}")]
        public async Task<IActionResult> GetReservationByCode(string reservationCode)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var reservation = await _reservationService.GetReservationByCodeAsync(userId, reservationCode);
            
            if (reservation == null)
                return NotFound(new { message = "Reservation not found or you don't have permission to view it." });
                
            return Ok(reservation);
        }

        // -------------------------------
        // 📋 GET station reservations (xem đặt chỗ của trạm)
        // -------------------------------
        [HttpGet("stations/{stationId}/reservations")]
        public async Task<IActionResult> GetStationReservations(int stationId, [FromQuery] int? pointId = null, [FromQuery] string? status = null, [FromQuery] DateTime? fromDate = null, [FromQuery] DateTime? toDate = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var filter = new ReservationFilter
            {
                StationId = stationId,
                PointId = pointId,
                Status = status,
                FromDate = fromDate,
                ToDate = toDate,
                Page = page,
                PageSize = pageSize
            };

            var reservations = await _reservationService.GetReservationsAsync(filter);
            return Ok(reservations);
        }

        // -------------------------------
        // 6️⃣ GET upcoming (các đặt chỗ sắp tới)
        // -------------------------------
        [HttpGet("upcoming")]
        public async Task<IActionResult> GetUpcoming([FromQuery] int hours = 48)
        {
            // Validation: hours phải > 0 và <= 8760 (1 năm)
            if (hours <= 0)
                return BadRequest(new { message = "Hours must be greater than 0." });
            
            if (hours > 8760) // 365 * 24 = 8760 hours = 1 year
                return BadRequest(new { message = "Hours cannot exceed 8760 (1 year)." });

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var upcoming = await _reservationService.GetUpcomingReservationsAsync(userId, TimeSpan.FromHours(hours));
            return Ok(upcoming);
        }

        // -------------------------------
        // 6️⃣.5 GET my-history (lịch sử đặt chỗ của tài xế)
        // -------------------------------
        [HttpGet("my-history")]
        public async Task<IActionResult> GetMyHistory(
            [FromQuery] string? status = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] int? stationId = null,
            [FromQuery] string? stationName = null,
            [FromQuery] string? stationAddress = null,
            [FromQuery] int? pointId = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                // Lấy userId từ JWT token
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

                // Validation
                if (page < 1) page = 1;
                if (pageSize <= 0 || pageSize > 200) pageSize = 20;

                // Gọi service để lấy lịch sử đặt chỗ
                var history = await _reservationService.GetReservationHistoryAsync(
                    userId,
                    status,
                    fromDate,
                    toDate,
                    stationId,
                    stationName,
                    stationAddress,
                    pointId,
                    page,
                    pageSize
                );

                return Ok(new
                {
                    data = history,
                    page = page,
                    pageSize = pageSize,
                    count = history.Count()
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Đã xảy ra lỗi khi lấy lịch sử đặt chỗ",
                    error = ex.Message
                });
            }
        }

        // -------------------------------
        // 7️⃣ CANCEL reservation (huỷ đặt chỗ bằng mã)
        // -------------------------------
        [HttpDelete("{reservationCode}")]
        public async Task<IActionResult> Cancel(string reservationCode, [FromQuery] string? reason = null)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var ok = await _reservationService.CancelReservationByCodeAsync(userId, reservationCode, reason);
            return ok ? NoContent() : NotFound();
        }

        // -------------------------------
        // 8️⃣ GENERATE QR Code (tạo mã QR bằng reservation code)
        // -------------------------------
        [HttpGet("{reservationCode}/qrcode")]
        public async Task<IActionResult> GetQRCode(string reservationCode)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var reservation = await _reservationService.GetReservationByCodeAsync(userId, reservationCode);
            
            if (reservation == null)
                return NotFound(new { message = "Reservation not found or you don't have permission to view it." });

            // Payload: nội dung mã QR tối ưu cho business logic
            // Format: multi-line format, mỗi trường một dòng để dễ đọc và parse
            // - Dùng tên đầy đủ (không viết tắt) để rõ ràng
            // - Mỗi trường xuống dòng để dễ debug và maintain
            // - StartTime dùng ISO 8601 format (YYYY-MM-DDTHH:mm:ss) để dễ đọc hơn Unix timestamp
            // Dùng ISO 8601 round-trip (UTC có hậu tố 'Z')
            var startTimeIso = reservation.StartTime.ToString("o");
            var stationId = reservation.ChargingPoint?.StationId ?? 0;
            var payload = $"EVCHG\nReservationCode={reservation.ReservationCode}\nPointId={reservation.PointId}\nStationId={stationId}\nStartTime={startTimeIso}";
            var png = _qrCodeService.GenerateQRCode(payload);

            return File(png, "image/png");
        }
    }
}
