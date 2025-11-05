using EVCharging.BE.Common.DTOs.Charging;
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
                    return BadRequest(new { message = "Invalid request data", errors = ModelState.Values.SelectMany(v => v.Errors) });
                }

                // ✅ Lấy userId từ JWT token
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                
                // ✅ Lấy driverId từ userId
                var driverProfile = await _db.DriverProfiles
                    .FirstOrDefaultAsync(d => d.UserId == userId);
                
                if (driverProfile == null)
                {
                    return BadRequest(new { 
                        message = "Driver profile not found. Please complete your driver profile first." 
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
                        return NotFound(new { message = $"Charging point with QR code '{request.PointQrCode}' not found." });
                    }

                    // Kiểm tra point có available không
                    if (chargingPoint.Status != "available")
                    {
                        return BadRequest(new { 
                            message = $"Charging point '{request.PointQrCode}' is currently {chargingPoint.Status}. Please choose another point." 
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
                        message = "Either PointQrCode or ChargingPointId must be provided." 
                    });
                }

                // ✅ Validate driver
                var driverIsValid = await _chargingService.ValidateDriverAsync(driverId);
                if (!driverIsValid)
                {
                    return BadRequest(new { message = "Driver profile not found or invalid. Please contact support." });
                }

                // ✅ Kiểm tra driver có session active không
                var activeSessions = await _chargingService.GetSessionsByDriverAsync(driverId);
                var hasActiveDriverSession = activeSessions.Any(s => s.Status == "in_progress");
                if (hasActiveDriverSession)
                {
                    return BadRequest(new { message = "You already have an active charging session. Please complete it first." });
                }

                // ✅ Validate charging point
                var pointIsAvailable = await _chargingService.ValidateChargingPointAsync(chargingPointId, now);
                if (!pointIsAvailable)
                {
                    return BadRequest(new { 
                        message = "The charging point is currently unavailable. It may be in use by another session or under maintenance." 
                    });
                }

                // ✅ Kiểm tra có thể start session không
                var canStart = await _chargingService.CanStartSessionAsync(chargingPointId, driverId, now);
                if (!canStart)
                {
                    return BadRequest(new { 
                        message = "Cannot start charging session. The charging point may be busy or you have restrictions." 
                    });
                }

                // ✅ Check upcoming reservation để giới hạn thời gian walk-in session
                var upcomingReservation = await _db.Reservations
                    .Where(r => r.PointId == chargingPointId 
                        && (r.Status == "booked" || r.Status == "checked_in") 
                        && r.StartTime > now)
                    .OrderBy(r => r.StartTime)
                    .FirstOrDefaultAsync();

                DateTime? maxEndTime = null;
                string? warningMessage = null;
                
                if (upcomingReservation != null)
                {
                    // Có reservation sắp đến, set maxEndTime = reservation.StartTime (trừ 5 phút buffer để đảm bảo)
                    var bufferMinutes = 5; // Buffer 5 phút trước khi reservation bắt đầu
                    maxEndTime = upcomingReservation.StartTime.AddMinutes(-bufferMinutes);
                    
                    var timeUntilReservation = (int)(upcomingReservation.StartTime - now).TotalMinutes;
                    warningMessage = $"Warning: There is a reservation starting at {upcomingReservation.StartTime:HH:mm}. Your walk-in session will automatically stop at {maxEndTime.Value:HH:mm} ({timeUntilReservation - bufferMinutes} minutes from now).";
                    
                    Console.WriteLine($"[StartWalkInSession] Upcoming reservation found - ReservationId={upcomingReservation.ReservationId}, StartTime={upcomingReservation.StartTime}, MaxEndTime={maxEndTime}");
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
                        message = "Failed to start charging session. Please try again or contact support." 
                    });
                }

                // ✅ Build response message với warning nếu có
                var responseMessage = "Charging session started successfully.";
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
                return StatusCode(500, new { message = "An error occurred while starting the charging session", error = ex.Message });
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
                    return BadRequest(new { message = "Invalid request data", errors = ModelState.Values.SelectMany(v => v.Errors) });
                }

                var result = await _chargingService.StopSessionAsync(request);
                if (result == null)
                {
                    return BadRequest(new { message = "Failed to stop charging session. Session may not exist or already completed." });
                }

                // Send real-time notification
                await _signalRService.NotifySessionCompletedAsync(result.SessionId, result);

                return Ok(new { message = "Charging session stopped successfully", data = result });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while stopping the charging session", error = ex.Message });
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
                    return NotFound(new { message = "Charging session not found" });
                }

                return Ok(new { data = result });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving the charging session", error = ex.Message });
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
                return StatusCode(500, new { message = "An error occurred while retrieving active sessions", error = ex.Message });
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
                return StatusCode(500, new { message = "An error occurred while retrieving driver sessions", error = ex.Message });
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
                return StatusCode(500, new { message = "An error occurred while retrieving station sessions", error = ex.Message });
            }
        }


        /// <summary>
        /// Lấy logs của phiên sạc
        /// </summary>
        /// <param name="sessionId">ID phiên sạc</param>
        /// <returns>Danh sách logs</returns>
        [HttpGet("{sessionId}/logs")]
        public async Task<IActionResult> GetSessionLogs(int sessionId)
        {
            try
            {
                var result = await _chargingService.GetSessionLogsAsync(sessionId);
                return Ok(new { data = result, count = result.Count() });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving session logs", error = ex.Message });
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
                return StatusCode(500, new { message = "An error occurred while estimating remaining time", error = ex.Message });
            }
        }
    }
}