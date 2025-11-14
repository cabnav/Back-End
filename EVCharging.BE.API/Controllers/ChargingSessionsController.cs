using EVCharging.BE.Common.DTOs.Charging;
using EVCharging.BE.Common.DTOs.Staff;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EVCharging.BE.Services.Services.Charging;
using EVCharging.BE.Services.Services.Notification;
using EVCharging.BE.DAL;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EVCharging.BE.API.Controllers
{
    /// <summary>
    /// Controller quản lý phiên sạc - Start/Stop/Status
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ChargingSessionsController : ControllerBase
    {
        private readonly IChargingService _chargingService;
        private readonly ISessionMonitorService _sessionMonitorService;
        private readonly ISignalRNotificationService _signalRService;
        private readonly EvchargingManagementContext _db;

        public ChargingSessionsController(
            IChargingService chargingService,
            ISessionMonitorService sessionMonitorService,
            ISignalRNotificationService signalRService,
            EvchargingManagementContext db)
        {
            _chargingService = chargingService;
            _sessionMonitorService = sessionMonitorService;
            _signalRService = signalRService;
            _db = db;
        }

        /// <summary>
        /// Bắt đầu phiên sạc walk-in cho driver đã có tài khoản (không có đặt chỗ)
        /// </summary>
        /// <param name="request">Thông tin bắt đầu sạc</param>
        /// <returns>Thông tin phiên sạc đã tạo</returns>
        [HttpPost("start")]
        public async Task<IActionResult> StartSession([FromBody] WalkInSessionStartRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new { message = "Dữ liệu yêu cầu không hợp lệ", errors = ModelState.Values.SelectMany(v => v.Errors) });
                }

                // ✅ Lấy userId từ JWT token
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                
                // ✅ Lấy driverId từ userId
                var driverProfile = await _db.DriverProfiles
                    .FirstOrDefaultAsync(d => d.UserId == userId);
                
                if (driverProfile == null)
                {
                    return BadRequest(new { 
                        message = "Không tìm thấy hồ sơ tài xế. Vui lòng hoàn thiện hồ sơ tài xế trước." 
                    });
                }

                var driverId = driverProfile.DriverId;
                var now = DateTime.UtcNow;

                // ✅ Walk-in session: Tìm charging point từ PointQrCode hoặc dùng ChargingPointId
                int chargingPointId;
                if (!string.IsNullOrEmpty(request.PointQrCode))
                {
                    // Tìm charging point từ QR code
                    var chargingPoint = await _db.ChargingPoints
                        .FirstOrDefaultAsync(p => p.QrCode == request.PointQrCode);
                    
                    if (chargingPoint == null)
                    {
                        return NotFound(new { message = $"Không tìm thấy điểm sạc với mã QR '{request.PointQrCode}'." });
                    }

                    // Kiểm tra point có available không
                    if (chargingPoint.Status != "available")
                    {
                        return BadRequest(new { 
                            message = $"Điểm sạc '{request.PointQrCode}' hiện đang ở trạng thái {chargingPoint.Status}. Vui lòng chọn điểm sạc khác." 
                        });
                    }

                    chargingPointId = chargingPoint.PointId;
                }
                else if (request.ChargingPointId.HasValue)
                {
                    chargingPointId = request.ChargingPointId.Value;
                }
                else
                {
                    // Nếu không có cả PointQrCode và ChargingPointId
                    return BadRequest(new { 
                        message = "Vui lòng cung cấp PointQrCode hoặc ChargingPointId." 
                    });
                }

                // ✅ Validate driver
                var driverIsValid = await _chargingService.ValidateDriverAsync(driverId);
                if (!driverIsValid)
                {
                    return BadRequest(new { message = "Hồ sơ tài xế không tìm thấy hoặc không hợp lệ. Vui lòng liên hệ hỗ trợ." });
                }

                // ✅ Kiểm tra driver có session active không
                var activeSessions = await _chargingService.GetSessionsByDriverAsync(driverId);
                var hasActiveDriverSession = activeSessions.Any(s => s.Status == "in_progress");
                if (hasActiveDriverSession)
                {
                    return BadRequest(new { message = "Bạn đã có một phiên sạc đang hoạt động. Vui lòng hoàn thành phiên sạc đó trước." });
                }

                // ✅ Validate charging point
                var pointIsAvailable = await _chargingService.ValidateChargingPointAsync(chargingPointId, now);
                if (!pointIsAvailable)
                {
                    return BadRequest(new { 
                        message = "Điểm sạc hiện không khả dụng. Có thể đang được sử dụng bởi phiên sạc khác hoặc đang bảo trì." 
                    });
                }

                // ✅ Kiểm tra có thể start session không
                var canStart = await _chargingService.CanStartSessionAsync(chargingPointId, driverId, now);
                if (!canStart)
                {
                    return BadRequest(new { 
                        message = "Không thể bắt đầu phiên sạc. Điểm sạc có thể đang bận hoặc bạn có hạn chế." 
                    });
                }

                // ✅ Check reservation để giới hạn thời gian walk-in session
                // 1) Check reservation sắp đến (trong tương lai)
                // 2) ✅ QUAN TRỌNG: Check reservation đã QUÁ start_time nhưng vẫn trong grace period (chưa check-in)
                const int NO_SHOW_GRACE_MINUTES = 30; // Grace period 30 phút sau start_time
                var gracePeriodStart = now.AddMinutes(-NO_SHOW_GRACE_MINUTES);
                
                // Check reservation sắp đến (trong tương lai)
                var upcomingReservation = await _db.Reservations
                    .Where(r => r.PointId == chargingPointId 
                        && (r.Status == "booked" || r.Status == "checked_in") 
                        && r.StartTime > now
                        && r.EndTime > now) // Reservation chưa kết thúc
                    .OrderBy(r => r.StartTime)
                    .FirstOrDefaultAsync();
                
                // ✅ Check reservation đã QUÁ start_time nhưng vẫn trong grace period (status="booked" chưa check-in)
                var activeReservation = await _db.Reservations
                    .Where(r => r.PointId == chargingPointId 
                        && r.Status == "booked" // Chỉ check reservation chưa check-in
                        && r.StartTime <= now // Đã quá start_time
                        && r.StartTime >= gracePeriodStart // Vẫn trong grace period (30 phút)
                        && r.EndTime > now) // Reservation chưa kết thúc
                    .OrderBy(r => r.StartTime)
                    .FirstOrDefaultAsync();

                DateTime? maxEndTime = null;
                string? warningMessage = null;
                
                // ✅ BLOCK walk-in nếu có reservation đang active (đã quá start_time nhưng vẫn trong grace period)
                if (activeReservation != null)
                {
                    var minutesSinceStart = (now - activeReservation.StartTime).TotalMinutes;
                    return BadRequest(new { 
                        message = $"Không thể bắt đầu phiên sạc walk-in. Có một đặt chỗ đang hoạt động (bắt đầu lúc {activeReservation.StartTime:HH:mm}, cách đây {minutesSinceStart:F0} phút) vẫn còn trong thời gian chờ. Người đặt chỗ có thể check-in bất cứ lúc nào." 
                    });
                }
                
                if (upcomingReservation != null)
                {
                    var timeUntilReservation = (upcomingReservation.StartTime - now).TotalMinutes;
                    const int MINIMUM_TIME_BEFORE_RESERVATION_MINUTES = 15; // Tối thiểu 15 phút trước reservation
                    
                    // ✅ BLOCK walk-in nếu reservation sắp đến quá gần (dưới 15 phút)
                    if (timeUntilReservation < MINIMUM_TIME_BEFORE_RESERVATION_MINUTES)
                    {
                        return BadRequest(new { 
                            message = $"Không thể bắt đầu phiên sạc walk-in. Có một đặt chỗ sẽ bắt đầu lúc {upcomingReservation.StartTime:HH:mm} (sau {timeUntilReservation:F0} phút). Vui lòng đợi hoặc sử dụng điểm sạc khác." 
                        });
                    }
                    
                    // Có reservation sắp đến nhưng còn đủ thời gian (>= 15 phút), set maxEndTime = reservation.StartTime (trừ 5 phút buffer để đảm bảo)
                    var bufferMinutes = 5; // Buffer 5 phút trước khi reservation bắt đầu
                    maxEndTime = upcomingReservation.StartTime.AddMinutes(-bufferMinutes);
                    
                    warningMessage = $"Cảnh báo: Có một đặt chỗ sẽ bắt đầu lúc {upcomingReservation.StartTime:HH:mm}. Phiên sạc walk-in của bạn sẽ tự động dừng lúc {maxEndTime.Value:HH:mm} (sau {timeUntilReservation - bufferMinutes:F0} phút nữa).";
                    
                    Console.WriteLine($"[StartWalkInSession] Upcoming reservation found - ReservationId={upcomingReservation.ReservationId}, StartTime={upcomingReservation.StartTime}, MaxEndTime={maxEndTime}, TimeUntilReservation={timeUntilReservation:F0} minutes");
                }

                // ✅ Convert WalkInSessionStartRequest sang ChargingSessionStartRequest
                var sessionRequest = new ChargingSessionStartRequest
                {
                    ChargingPointId = chargingPointId,
                    DriverId = driverId, // ✅ Dùng driverId từ JWT token
                    InitialSOC = request.InitialSOC,
                    PointQrCode = request.PointQrCode,
                    QrCode = request.PointQrCode, // ✅ Set QrCode cho backward compatibility
                    Notes = request.Notes,
                    StartAtUtc = now, // ✅ Walk-in session luôn bắt đầu ngay lập tức
                    MaxEndTimeUtc = maxEndTime // ✅ Set maxEndTime nếu có reservation sắp đến
                };

                // ✅ Bắt đầu phiên sạc
                var result = await _chargingService.StartSessionAsync(sessionRequest);
                if (result == null)
                {
                    return BadRequest(new { 
                        message = "Không thể bắt đầu phiên sạc. Vui lòng thử lại hoặc liên hệ hỗ trợ." 
                    });
                }

                // ✅ Build response message với warning nếu có
                var responseMessage = "Phiên sạc đã được bắt đầu thành công.";
                if (!string.IsNullOrEmpty(warningMessage))
                {
                    responseMessage += $" {warningMessage}";
                }

                // Send real-time notification
                await _signalRService.NotifySessionUpdateAsync(result.SessionId, result);

                return Ok(new { 
                    message = responseMessage, 
                    data = result,
                    warning = warningMessage 
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi bắt đầu phiên sạc", error = ex.Message });
            }
        }


        /// <summary>
        /// Dừng phiên sạc
        /// </summary>
        /// <param name="request">Thông tin dừng sạc</param>
        /// <returns>Thông tin phiên sạc đã dừng</returns>
        [HttpPost("stop")]
        public async Task<IActionResult> StopSession([FromBody] ChargingSessionStopRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new { message = "Dữ liệu yêu cầu không hợp lệ", errors = ModelState.Values.SelectMany(v => v.Errors) });
                }

                var result = await _chargingService.StopSessionAsync(request);
                if (result == null)
                {
                    return BadRequest(new { message = "Không thể dừng phiên sạc. Phiên sạc có thể không tồn tại hoặc đã hoàn thành." });
                }

                // Send real-time notification
                await _signalRService.NotifySessionCompletedAsync(result.SessionId, result);

                return Ok(new { message = "Phiên sạc đã được dừng thành công", data = result });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi dừng phiên sạc", error = ex.Message });
            }
        }


        /// <summary>
        /// Lấy thông tin phiên sạc theo ID
        /// </summary>
        /// <param name="sessionId">ID phiên sạc</param>
        /// <returns>Thông tin phiên sạc</returns>
        [HttpGet("{sessionId}")]
        public async Task<IActionResult> GetSession(int sessionId)
        {
            try
            {
                var result = await _chargingService.GetSessionByIdAsync(sessionId);
                if (result == null)
                {
                    return NotFound(new { message = "Không tìm thấy phiên sạc" });
                }

                return Ok(new { data = result });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi lấy thông tin phiên sạc", error = ex.Message });
            }
        }

        /// <summary>
        /// Lấy danh sách phiên sạc đang hoạt động
        /// </summary>
        /// <returns>Danh sách phiên sạc đang hoạt động</returns>
        [HttpGet("active")]
        public async Task<IActionResult> GetActiveSessions()
        {
            try
            {
                var result = await _chargingService.GetActiveSessionsAsync();
                return Ok(new { data = result, count = result.Count() });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi lấy danh sách phiên sạc đang hoạt động", error = ex.Message });
            }
        }

        /// <summary>
        /// Lấy danh sách phiên sạc theo driver
        /// </summary>
        /// <param name="driverId">ID driver</param>
        /// <returns>Danh sách phiên sạc của driver</returns>
        [HttpGet("driver/{driverId}")]
        public async Task<IActionResult> GetSessionsByDriver(int driverId)
        {
            try
            {
                var result = await _chargingService.GetSessionsByDriverAsync(driverId);
                return Ok(new { data = result, count = result.Count() });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi lấy danh sách phiên sạc của tài xế", error = ex.Message });
            }
        }

        /// <summary>
        /// Lấy danh sách phiên sạc theo trạm
        /// </summary>
        /// <param name="stationId">ID trạm</param>
        /// <returns>Danh sách phiên sạc của trạm</returns>
        [HttpGet("station/{stationId}")]
        public async Task<IActionResult> GetSessionsByStation(int stationId)
        {
            try
            {
                var result = await _chargingService.GetSessionsByStationAsync(stationId);
                return Ok(new { data = result, count = result.Count() });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi lấy danh sách phiên sạc của trạm", error = ex.Message });
            }
        }

        /// <summary>
        /// Lấy lịch sử phiên sạc của tài xế (tự động lấy từ JWT token)
        /// </summary>
        /// <param name="status">Lọc theo trạng thái (in_progress, completed, cancelled, etc.)</param>
        /// <param name="fromDate">Lọc từ ngày</param>
        /// <param name="toDate">Lọc đến ngày</param>
        /// <param name="stationId">Lọc theo ID trạm</param>
        /// <param name="stationName">Lọc theo tên trạm (tìm kiếm gần đúng)</param>
        /// <param name="stationAddress">Lọc theo địa chỉ trạm (tìm kiếm gần đúng)</param>
        /// <param name="pointId">Lọc theo ID điểm sạc</param>
        /// <param name="page">Số trang (mặc định: 1)</param>
        /// <param name="pageSize">Số bản ghi mỗi trang (mặc định: 20, tối đa: 200)</param>
        /// <returns>Danh sách phiên sạc của tài xế</returns>
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

                // Gọi service để lấy lịch sử phiên sạc
                var history = await _chargingService.GetSessionHistoryAsync(
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
                    message = "Đã xảy ra lỗi khi lấy lịch sử phiên sạc",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Lấy log mới nhất của phiên sạc (tự động lấy từ userId trong JWT token, không cần sessionId)
        /// </summary>
        /// <returns>Log mới nhất và thông tin monitoring của session đang active</returns>
        [HttpGet("logs")]
        public async Task<IActionResult> GetSessionLogs()
        {
            try
            {
                // ✅ Lấy userId từ JWT token
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                
                // ✅ Lấy driverId từ userId
                var driverProfile = await _db.DriverProfiles
                    .FirstOrDefaultAsync(d => d.UserId == userId);
                
                if (driverProfile == null)
                {
                    return BadRequest(new { 
                        message = "Không tìm thấy hồ sơ tài xế. Vui lòng hoàn thiện hồ sơ tài xế trước." 
                    });
                }

                var driverId = driverProfile.DriverId;

                // ✅ Tìm session đang active của driver (ưu tiên session đang sạc)
                var activeSession = await _db.ChargingSessions
                    .Where(s => s.DriverId == driverId && s.Status == "in_progress")
                    .OrderByDescending(s => s.StartTime)
                    .FirstOrDefaultAsync();

                // Nếu không có session active, lấy session mới nhất
                if (activeSession == null)
                {
                    activeSession = await _db.ChargingSessions
                        .Where(s => s.DriverId == driverId)
                        .OrderByDescending(s => s.StartTime)
                        .FirstOrDefaultAsync();
                }

                if (activeSession == null)
                {
                    return NotFound(new { 
                        message = "Không tìm thấy phiên sạc cho người dùng này." 
                    });
                }

                var sessionId = activeSession.SessionId;
                var logs = await _chargingService.GetSessionLogsAsync(sessionId);
                var monitoringStatus = await _sessionMonitorService.GetMonitoringStatusAsync(sessionId);
                
                // Lấy log cuối cùng (log mới nhất)
                var lastLog = logs.OrderByDescending(l => l.LogTime).FirstOrDefault();
                
                // Trả về chỉ 1 log mới nhất trong mảng data
                var logList = lastLog != null ? new List<SessionLogDTO> { lastLog } : new List<SessionLogDTO>();
                
                return Ok(new { 
                    data = logList,
                    count = logList.Count,
                    sessionId = sessionId,
                    sessionStatus = activeSession.Status,
                    monitoring = monitoringStatus,
                    summary = new
                    {
                        totalLogs = logs.Count(),
                        lastLogTime = lastLog?.LogTime,
                        currentSOC = lastLog?.SOCPercentage,
                        currentPower = lastLog?.CurrentPower,
                        voltage = lastLog?.Voltage,
                        temperature = lastLog?.Temperature
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi lấy logs phiên sạc", error = ex.Message });
            }
        }

        /// <summary>
        /// Kiểm tra trạng thái monitoring và logs của session
        /// </summary>
        /// <param name="sessionId">ID phiên sạc</param>
        /// <returns>Trạng thái monitoring và thông tin logs</returns>
        [HttpGet("{sessionId}/monitoring-status")]
        public async Task<IActionResult> GetMonitoringStatus(int sessionId)
        {
            try
            {
                var monitoringStatus = await _sessionMonitorService.GetMonitoringStatusAsync(sessionId);
                var session = await _chargingService.GetSessionByIdAsync(sessionId);
                
                if (session == null)
                {
                    return NotFound(new { message = "Không tìm thấy phiên sạc" });
                }
                
                return Ok(new { 
                    data = monitoringStatus,
                    session = session
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi lấy trạng thái monitoring", error = ex.Message });
            }
        }




        /// <summary>
        /// Ước tính thời gian còn lại
        /// </summary>
        /// <param name="sessionId">ID phiên sạc</param>
        /// <param name="targetSOC">SOC mục tiêu</param>
        /// <returns>Thời gian còn lại ước tính</returns>
        [HttpGet("{sessionId}/estimate-time")]
        public async Task<IActionResult> EstimateRemainingTime(int sessionId, [FromQuery] int targetSOC = 100)
        {
            try
            {
                var result = await _sessionMonitorService.EstimateRemainingTimeAsync(sessionId, targetSOC);
                return Ok(new { 
                    data = new { 
                        sessionId, 
                        targetSOC, 
                        estimatedTime = result.ToString(@"hh\:mm\:ss"),
                        estimatedMinutes = (int)result.TotalMinutes
                    } 
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi ước tính thời gian còn lại", error = ex.Message });
            }
        }

        /// <summary>
        /// Tạo báo cáo sự cố (cho driver)
        /// POST /api/chargingsessions/incidents
        /// </summary>
        [HttpPost("incidents")]
        public async Task<IActionResult> CreateIncidentReport([FromBody] CreateIncidentReportRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new { 
                        message = "Dữ liệu yêu cầu không hợp lệ", 
                        errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)) 
                    });
                }

                // Lấy userId từ JWT token
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

                // Lấy driverId từ userId để đảm bảo user là driver
                var driverProfile = await _db.DriverProfiles
                    .FirstOrDefaultAsync(d => d.UserId == userId);
                
                if (driverProfile == null)
                {
                    return BadRequest(new { 
                        message = "Không tìm thấy hồ sơ tài xế. Vui lòng hoàn thiện hồ sơ tài xế trước." 
                    });
                }

                var result = await _chargingService.CreateIncidentReportAsync(userId, request);
                if (result == null)
                {
                    return BadRequest(new { 
                        message = "Không thể tạo báo cáo sự cố. Điểm sạc có thể không tồn tại hoặc đã xảy ra lỗi." 
                    });
                }

                return Ok(new { 
                    message = "Báo cáo sự cố đã được tạo thành công", 
                    data = result 
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    message = "Đã xảy ra lỗi khi tạo báo cáo sự cố", 
                    error = ex.Message 
                });
            }
        }

        /// <summary>
        /// Lấy danh sách báo cáo sự cố của driver (tự động lấy từ JWT token)
        /// GET /api/chargingsessions/incidents?status={status}&priority={priority}&page={page}&pageSize={pageSize}
        /// </summary>
        [HttpGet("incidents")]
        public async Task<IActionResult> GetMyIncidentReports(
            [FromQuery] string status = "all",
            [FromQuery] string priority = "all",
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                // Lấy userId từ JWT token
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

                // Validate pagination
                if (page < 1) page = 1;
                if (pageSize < 1 || pageSize > 100) pageSize = 20;

                var reports = await _chargingService.GetDriverIncidentReportsAsync(userId, status, priority, page, pageSize);
                
                return Ok(new { 
                    message = "Danh sách báo cáo sự cố đã được lấy thành công", 
                    data = reports,
                    count = reports.Count(),
                    page = page,
                    pageSize = pageSize,
                    filters = new
                    {
                        status = status,
                        priority = priority
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    message = "Đã xảy ra lỗi khi lấy danh sách báo cáo sự cố", 
                    error = ex.Message 
                });
            }
        }
    }
}