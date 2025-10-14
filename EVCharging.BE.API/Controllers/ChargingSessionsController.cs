using EVCharging.BE.Common.DTOs.Charging;
using EVCharging.BE.Services.Services;
using EVCharging.BE.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
        private readonly ICostCalculationService _costCalculationService;
        private readonly ISessionMonitorService _sessionMonitorService;
        private readonly ISignalRNotificationService _signalRService;

        public ChargingSessionsController(
            IChargingService chargingService,
            ICostCalculationService costCalculationService,
            ISessionMonitorService sessionMonitorService,
            ISignalRNotificationService signalRService)
        {
            _chargingService = chargingService;
            _costCalculationService = costCalculationService;
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
                    return BadRequest(new { message = "Failed to start charging session. Please check charging point availability and driver status." });
                }

                // Send real-time notification
                await _signalRService.NotifySessionUpdateAsync(result.SessionId, result);

                return Ok(new { message = "Charging session started successfully", data = result });
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
        /// Cập nhật trạng thái phiên sạc
        /// </summary>
        /// <param name="request">Thông tin cập nhật trạng thái</param>
        /// <returns>Thông tin phiên sạc đã cập nhật</returns>
        [HttpPut("status")]
        public async Task<IActionResult> UpdateSessionStatus([FromBody] ChargingSessionStatusRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new { message = "Invalid request data", errors = ModelState.Values.SelectMany(v => v.Errors) });
                }

                var result = await _chargingService.UpdateSessionStatusAsync(request);
                if (result == null)
                {
                    return BadRequest(new { message = "Failed to update session status. Session may not exist." });
                }

                return Ok(new { message = "Session status updated successfully", data = result });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while updating session status", error = ex.Message });
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
        /// Tạo log cho phiên sạc
        /// </summary>
        /// <param name="request">Thông tin log</param>
        /// <returns>Kết quả tạo log</returns>
        [HttpPost("logs")]
        public async Task<IActionResult> CreateSessionLog([FromBody] SessionLogCreateRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new { message = "Invalid request data", errors = ModelState.Values.SelectMany(v => v.Errors) });
                }

                var result = await _chargingService.CreateSessionLogAsync(request);
                if (!result)
                {
                    return BadRequest(new { message = "Failed to create session log" });
                }

                return Ok(new { message = "Session log created successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while creating session log", error = ex.Message });
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
        /// Tính toán chi phí sạc
        /// </summary>
        /// <param name="request">Thông tin tính toán chi phí</param>
        /// <returns>Kết quả tính toán chi phí</returns>
        [HttpPost("calculate-cost")]
        public async Task<IActionResult> CalculateCost([FromBody] CostCalculationRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new { message = "Invalid request data", errors = ModelState.Values.SelectMany(v => v.Errors) });
                }

                var result = await _costCalculationService.CalculateCostAsync(request);
                return Ok(new { data = result });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while calculating cost", error = ex.Message });
            }
        }

        /// <summary>
        /// Lấy trạng thái real-time của phiên sạc
        /// </summary>
        /// <param name="sessionId">ID phiên sạc</param>
        /// <returns>Trạng thái real-time</returns>
        [HttpGet("{sessionId}/status")]
        public async Task<IActionResult> GetSessionStatus(int sessionId)
        {
            try
            {
                var result = await _sessionMonitorService.GetSessionStatusAsync(sessionId);
                if (result == null)
                {
                    return NotFound(new { message = "Session not found or not active" });
                }

                return Ok(new { data = result });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving session status", error = ex.Message });
            }
        }

        /// <summary>
        /// Lấy analytics của phiên sạc
        /// </summary>
        /// <param name="sessionId">ID phiên sạc</param>
        /// <returns>Analytics data</returns>
        [HttpGet("{sessionId}/analytics")]
        public async Task<IActionResult> GetSessionAnalytics(int sessionId)
        {
            try
            {
                var result = await _sessionMonitorService.GetSessionAnalyticsAsync(sessionId);
                return Ok(new { data = result });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving session analytics", error = ex.Message });
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