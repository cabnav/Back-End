using EVCharging.BE.Common.DTOs.Charging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EVCharging.BE.Services.Services.Charging;
using EVCharging.BE.Services.Services.Notification;

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

        public ChargingSessionsController(
            IChargingService chargingService,
            ISessionMonitorService sessionMonitorService,
            ISignalRNotificationService signalRService)
        {
            _chargingService = chargingService;
            _sessionMonitorService = sessionMonitorService;
            _signalRService = signalRService;
        }

        /// <summary>
        /// Bắt đầu phiên sạc
        /// </summary>
        /// <param name="request">Thông tin bắt đầu sạc</param>
        /// <returns>Thông tin phiên sạc đã tạo</returns>
        [HttpPost("start")]
        public async Task<IActionResult> StartSession([FromBody] ChargingSessionStartRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new { message = "Invalid request data", errors = ModelState.Values.SelectMany(v => v.Errors) });
                }

                var result = await _chargingService.StartSessionAsync(request);
                if (result == null)
                {
                    // Kiểm tra chi tiết để có thông báo lỗi rõ ràng hơn
                    var pointIsAvailable = await _chargingService.ValidateChargingPointAsync(request.ChargingPointId, request.StartAtUtc);
                    var driverIsValid = await _chargingService.ValidateDriverAsync(request.DriverId);
                    var canStart = await _chargingService.CanStartSessionAsync(request.ChargingPointId, request.DriverId, request.StartAtUtc);
                    var activeSessions = await _chargingService.GetSessionsByDriverAsync(request.DriverId);
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
                    else if (!pointIsAvailable)
                    {
                        errorMessage = "The charging point is currently unavailable. It may be in use by another session or under maintenance.";
                    }
                    else if (!canStart)
                    {
                        errorMessage = "Cannot start charging session. The charging point may be busy or you have restrictions.";
                    }
                    else
                    {
                        errorMessage = "Failed to start charging session. Please try again or contact support.";
                    }
                    
                    return BadRequest(new { 
                        message = errorMessage,
                        pointId = request.ChargingPointId,
                        driverId = request.DriverId,
                        pointAvailable = pointIsAvailable,
                        driverValid = driverIsValid,
                        canStart = canStart,
                        hasActiveDriverSession = hasActiveDriverSession,
                        startAtUtc = request.StartAtUtc
                    });
                }

                // Send real-time notification
                await _signalRService.NotifySessionUpdateAsync(result.SessionId, result);

                return Ok(new { message = "Charging session started successfully", data = result });
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