using EVCharging.BE.Common.DTOs.Charging;
using EVCharging.BE.Services.Services.Charging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EVCharging.BE.API.Controllers
{
    /// <summary>
    /// Controller for real-time charging session monitoring
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class RealTimeChargingController : ControllerBase
    {
        private readonly IRealTimeChargingService _realTimeChargingService;

        public RealTimeChargingController(IRealTimeChargingService realTimeChargingService)
        {
            _realTimeChargingService = realTimeChargingService;
        }

        /// <summary>
        /// Get real-time session data including SOC and remaining time
        /// </summary>
        /// <param name="sessionId">Charging session ID</param>
        /// <returns>Real-time session data</returns>
        [HttpGet("session/{sessionId}")]
        public async Task<ActionResult<RealTimeSessionDTO>> GetRealTimeSession(int sessionId)
        {
            try
            {
                var session = await _realTimeChargingService.GetRealTimeSessionAsync(sessionId);
                if (session == null)
                    return NotFound(new { message = "Session not found" });

                return Ok(session);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving real-time session data", error = ex.Message });
            }
        }

        /// <summary>
        /// Update session with current SOC and power data
        /// </summary>
        /// <param name="sessionId">Charging session ID</param>
        /// <param name="currentSOC">Current State of Charge percentage</param>
        /// <param name="currentPower">Current power output in kW</param>
        /// <returns>Update result</returns>
        [HttpPut("session/{sessionId}/update")]
        public async Task<ActionResult> UpdateSessionData(
            [FromRoute] int sessionId,
            [FromBody] SessionUpdateRequest request)
        {
            try
            {
                var success = await _realTimeChargingService.UpdateSessionDataAsync(
                    sessionId, 
                    request.CurrentSOC, 
                    request.CurrentPower);

                if (!success)
                    return NotFound(new { message = "Session not found or update failed" });

                return Ok(new { message = "Session data updated successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error updating session data", error = ex.Message });
            }
        }

        /// <summary>
        /// Calculate estimated remaining time for charging
        /// </summary>
        /// <param name="sessionId">Charging session ID</param>
        /// <param name="currentSOC">Current State of Charge percentage</param>
        /// <param name="targetSOC">Target State of Charge percentage</param>
        /// <returns>Estimated remaining time in minutes</returns>
        [HttpGet("session/{sessionId}/remaining-time")]
        public async Task<ActionResult<int?>> GetRemainingTime(
            [FromRoute] int sessionId,
            [FromQuery] int currentSOC,
            [FromQuery] int? targetSOC = null)
        {
            try
            {
                var remainingTime = await _realTimeChargingService.CalculateRemainingTimeAsync(
                    sessionId, currentSOC, targetSOC);

                return Ok(new { remainingTimeMinutes = remainingTime });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error calculating remaining time", error = ex.Message });
            }
        }

        /// <summary>
        /// Get all active charging sessions for a driver
        /// </summary>
        /// <param name="driverId">Driver ID</param>
        /// <returns>List of active sessions</returns>
        [HttpGet("driver/{driverId}/active-sessions")]
        public async Task<ActionResult<IEnumerable<RealTimeSessionDTO>>> GetActiveSessions(int driverId)
        {
            try
            {
                var sessions = await _realTimeChargingService.GetActiveSessionsAsync(driverId);
                return Ok(sessions);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving active sessions", error = ex.Message });
            }
        }

        /// <summary>
        /// Check if charging is complete and send notifications
        /// </summary>
        /// <param name="sessionId">Charging session ID</param>
        /// <returns>Completion status</returns>
        [HttpPost("session/{sessionId}/check-completion")]
        public async Task<ActionResult> CheckChargingCompletion(int sessionId)
        {
            try
            {
                var isComplete = await _realTimeChargingService.CheckChargingCompletionAsync(sessionId);
                
                return Ok(new { 
                    isComplete,
                    message = isComplete ? "Charging completed and notifications sent" : "Charging still in progress"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error checking charging completion", error = ex.Message });
            }
        }

        /// <summary>
        /// Get real-time SOC percentage for a session
        /// </summary>
        /// <param name="sessionId">Charging session ID</param>
        /// <returns>Current SOC percentage</returns>
        [HttpGet("session/{sessionId}/soc")]
        public async Task<ActionResult<int>> GetCurrentSOC(int sessionId)
        {
            try
            {
                var session = await _realTimeChargingService.GetRealTimeSessionAsync(sessionId);
                if (session == null)
                    return NotFound(new { message = "Session not found" });

                return Ok(new { currentSOC = session.CurrentSOC });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving SOC data", error = ex.Message });
            }
        }

        /// <summary>
        /// Send charging completion notification manually
        /// </summary>
        /// <param name="sessionId">Charging session ID</param>
        /// <returns>Notification result</returns>
        [HttpPost("session/{sessionId}/send-notification")]
        public async Task<ActionResult> SendChargingCompletionNotification(int sessionId)
        {
            try
            {
                var isComplete = await _realTimeChargingService.CheckChargingCompletionAsync(sessionId);
                
                if (isComplete)
                {
                    return Ok(new { 
                        success = true,
                        message = "Charging completion notification sent successfully",
                        sessionId = sessionId,
                        timestamp = DateTime.UtcNow
                    });
                }
                else
                {
                    return BadRequest(new { 
                        success = false,
                        message = "Charging session is not complete yet",
                        sessionId = sessionId
                    });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    success = false,
                    message = "Error sending notification", 
                    error = ex.Message 
                });
            }
        }
    }

    /// <summary>
    /// Request model for session data updates
    /// </summary>
    public class SessionUpdateRequest
    {
        public int CurrentSOC { get; set; }
        public double CurrentPower { get; set; }
    }
}
