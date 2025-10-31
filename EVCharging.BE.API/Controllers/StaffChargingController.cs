using EVCharging.BE.Common.DTOs.Staff;
using EVCharging.BE.Services.Services.Staff;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EVCharging.BE.API.Controllers
{
    /// <summary>
    /// Controller quản lý phiên sạc từ góc độ Staff (Nhân viên trạm sạc)
    /// </summary>
    [Route("api/staff/charging")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class StaffChargingController : ControllerBase
    {
        private readonly IStaffChargingService _staffChargingService;

        public StaffChargingController(IStaffChargingService staffChargingService)
        {
            _staffChargingService = staffChargingService;
        }

        /// <summary>
        /// Lấy thông tin trạm được assigned (Dashboard)
        /// GET /api/staff/charging/my-station
        /// </summary>
        [HttpGet("my-station")]
        public async Task<IActionResult> GetMyStationInfo()
        {
            try
            {
                var staffId = GetStaffIdFromToken();
                if (staffId == 0)
                    return Unauthorized(new { message = "Invalid staff token" });

                var stationInfo = await _staffChargingService.GetMyStationInfoAsync(staffId);
                if (stationInfo == null)
                    return NotFound(new { message = "No station assigned to this staff member or shift not active" });

                return Ok(new { 
                    message = "Station info retrieved successfully", 
                    data = stationInfo 
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    message = "An error occurred while retrieving station info", 
                    error = ex.Message 
                });
            }
        }

        /// <summary>
        /// Khởi động phiên sạc cho khách walk-in (không có app)
        /// POST /api/staff/charging/walk-in/start
        /// </summary>
        [HttpPost("walk-in/start")]
        public async Task<IActionResult> StartWalkInSession([FromBody] WalkInSessionRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new { 
                        message = "Invalid request data", 
                        errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)) 
                    });
                }

                var staffId = GetStaffIdFromToken();
                if (staffId == 0)
                    return Unauthorized(new { message = "Invalid staff token" });

                var result = await _staffChargingService.StartWalkInSessionAsync(staffId, request);
                if (result == null)
                {
                    return BadRequest(new { 
                        message = "Failed to start walk-in session. Please check charging point availability and staff assignment." 
                    });
                }

                return Ok(new { 
                    message = "Walk-in charging session started successfully", 
                    data = result 
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    message = "An error occurred while starting walk-in session", 
                    error = ex.Message 
                });
            }
        }

        /// <summary>
        /// Dừng khẩn cấp phiên sạc
        /// POST /api/staff/charging/sessions/{sessionId}/emergency-stop
        /// </summary>
        [HttpPost("sessions/{sessionId}/emergency-stop")]
        public async Task<IActionResult> EmergencyStopSession(int sessionId, [FromBody] EmergencyStopRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new { 
                        message = "Invalid request data", 
                        errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)) 
                    });
                }

                var staffId = GetStaffIdFromToken();
                if (staffId == 0)
                    return Unauthorized(new { message = "Invalid staff token" });

                var result = await _staffChargingService.EmergencyStopSessionAsync(staffId, sessionId, request);
                if (result == null)
                {
                    return BadRequest(new { 
                        message = "Failed to emergency stop session. Session may not exist or you don't have access to this station." 
                    });
                }

                return Ok(new { 
                    message = "Session emergency stopped successfully", 
                    data = result,
                    incident = new 
                    { 
                        created = true,
                        message = "Incident report has been created automatically" 
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    message = "An error occurred while emergency stopping session", 
                    error = ex.Message 
                });
            }
        }

        /// <summary>
        /// Tạm dừng phiên sạc
        /// POST /api/staff/charging/sessions/{sessionId}/pause
        /// </summary>
        [HttpPost("sessions/{sessionId}/pause")]
        public async Task<IActionResult> PauseSession(int sessionId, [FromBody] PauseSessionRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new { 
                        message = "Invalid request data", 
                        errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)) 
                    });
                }

                var staffId = GetStaffIdFromToken();
                if (staffId == 0)
                    return Unauthorized(new { message = "Invalid staff token" });

                var result = await _staffChargingService.PauseSessionAsync(staffId, sessionId, request);
                if (result == null)
                {
                    return BadRequest(new { 
                        message = "Failed to pause session. Session may not exist, not active, or you don't have access." 
                    });
                }

                return Ok(new { 
                    message = "Session paused successfully", 
                    data = result,
                    warning = $"Session will be auto-cancelled after {request.MaxPauseDuration} minutes if not resumed"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    message = "An error occurred while pausing session", 
                    error = ex.Message 
                });
            }
        }

        /// <summary>
        /// Tiếp tục phiên sạc đã tạm dừng
        /// POST /api/staff/charging/sessions/{sessionId}/resume
        /// </summary>
        [HttpPost("sessions/{sessionId}/resume")]
        public async Task<IActionResult> ResumeSession(int sessionId, [FromBody] ResumeSessionRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new { 
                        message = "Invalid request data", 
                        errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)) 
                    });
                }

                var staffId = GetStaffIdFromToken();
                if (staffId == 0)
                    return Unauthorized(new { message = "Invalid staff token" });

                var result = await _staffChargingService.ResumeSessionAsync(staffId, sessionId, request);
                if (result == null)
                {
                    return BadRequest(new { 
                        message = "Failed to resume session. Session may not exist, not paused, or you don't have access." 
                    });
                }

                return Ok(new { 
                    message = "Session resumed successfully", 
                    data = result 
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    message = "An error occurred while resuming session", 
                    error = ex.Message 
                });
            }
        }

        /// <summary>
        /// Lấy danh sách phiên sạc tại trạm của staff (Dashboard với filtering)
        /// GET /api/staff/charging/sessions
        /// </summary>
        [HttpGet("sessions")]
        public async Task<IActionResult> GetMyStationSessions([FromQuery] StaffSessionsFilterRequest filter)
        {
            try
            {
                var staffId = GetStaffIdFromToken();
                if (staffId == 0)
                    return Unauthorized(new { message = "Invalid staff token" });

                var dashboard = await _staffChargingService.GetMyStationSessionsAsync(staffId, filter);
                
                return Ok(new { 
                    message = "Sessions retrieved successfully", 
                    data = dashboard 
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    message = "An error occurred while retrieving sessions", 
                    error = ex.Message 
                });
            }
        }

        /// <summary>
        /// Lấy chi tiết phiên sạc cụ thể
        /// GET /api/staff/charging/sessions/{sessionId}
        /// </summary>
        [HttpGet("sessions/{sessionId}")]
        public async Task<IActionResult> GetSessionDetail(int sessionId)
        {
            try
            {
                var staffId = GetStaffIdFromToken();
                if (staffId == 0)
                    return Unauthorized(new { message = "Invalid staff token" });

                var session = await _staffChargingService.GetSessionDetailAsync(staffId, sessionId);
                if (session == null)
                {
                    return NotFound(new { 
                        message = "Session not found or you don't have access to this session" 
                    });
                }

                return Ok(new { 
                    message = "Session details retrieved successfully", 
                    data = session 
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    message = "An error occurred while retrieving session details", 
                    error = ex.Message 
                });
            }
        }

        /// <summary>
        /// Kiểm tra staff có access vào station không
        /// GET /api/staff/charging/verify-access/{stationId}
        /// </summary>
        [HttpGet("verify-access/{stationId}")]
        public async Task<IActionResult> VerifyStationAccess(int stationId)
        {
            try
            {
                var staffId = GetStaffIdFromToken();
                if (staffId == 0)
                    return Unauthorized(new { message = "Invalid staff token" });

                var hasAccess = await _staffChargingService.VerifyStaffAssignmentAsync(staffId, stationId);
                
                return Ok(new { 
                    hasAccess = hasAccess,
                    message = hasAccess 
                        ? "Staff has access to this station" 
                        : "Staff does not have access to this station or shift not active"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    message = "An error occurred while verifying access", 
                    error = ex.Message 
                });
            }
        }

        /// <summary>
        /// Lấy danh sách stations được assigned
        /// GET /api/staff/charging/my-stations
        /// </summary>
        [HttpGet("my-stations")]
        public async Task<IActionResult> GetMyStations()
        {
            try
            {
                var staffId = GetStaffIdFromToken();
                if (staffId == 0)
                    return Unauthorized(new { message = "Invalid staff token" });

                var stationIds = await _staffChargingService.GetAssignedStationsAsync(staffId);
                
                return Ok(new { 
                    message = "Assigned stations retrieved successfully", 
                    data = stationIds,
                    count = stationIds.Count 
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    message = "An error occurred while retrieving assigned stations", 
                    error = ex.Message 
                });
            }
        }

        // ========== HELPER METHODS ==========

        /// <summary>
        /// Lấy Staff ID từ JWT token
        /// </summary>
        private int GetStaffIdFromToken()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int staffId))
            {
                return staffId;
            }
            return 0;
        }
    }
}

