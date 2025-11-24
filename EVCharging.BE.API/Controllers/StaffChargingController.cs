using EVCharging.BE.Common.DTOs.Payments;
using EVCharging.BE.Common.DTOs.Staff;
using EVCharging.BE.DAL;
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
    [Authorize(Policy = "StaffOrAdminPolicy")]
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
        /// Lấy danh sách tất cả điểm sạc tại trạm được assigned với đầy đủ thông tin real-time
        /// GET /api/staff/charging/points
        /// </summary>
        [HttpGet("points")]
        public async Task<IActionResult> GetMyStationChargingPoints()
        {
            try
            {
                var staffId = GetStaffIdFromToken();
                if (staffId == 0)
                    return Unauthorized(new { message = "Invalid staff token" });

                var chargingPoints = await _staffChargingService.GetMyStationChargingPointsAsync(staffId);
                
                return Ok(new { 
                    message = "Charging points retrieved successfully", 
                    data = chargingPoints,
                    count = chargingPoints.Count,
                    note = "Includes real-time status, power output, and active session information"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    message = "An error occurred while retrieving charging points", 
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
        /// Lấy danh sách phiên sạc đang hoạt động với tiến trình real-time
        /// GET /api/staff/charging/active-sessions
        /// </summary>
        [HttpGet("active-sessions")]
        public async Task<IActionResult> GetActiveSessionsProgress()
        {
            try
            {
                var staffId = GetStaffIdFromToken();
                if (staffId == 0)
                    return Unauthorized(new { message = "Invalid staff token" });

                var activeSessions = await _staffChargingService.GetActiveSessionsProgressAsync(staffId);
                
                return Ok(new { 
                    message = "Active sessions retrieved successfully", 
                    data = activeSessions,
                    count = activeSessions.Count,
                    note = "Only shows sessions with status 'in_progress' or 'paused' at assigned stations"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    message = "An error occurred while retrieving active sessions", 
                    error = ex.Message 
                });
            }
        }

        /// <summary>
        /// Lấy danh sách payments pending tại trạm của staff (để xác nhận thanh toán)
        /// GET /api/staff/charging/payments/pending
        /// </summary>
        [HttpGet("payments/pending")]
        public async Task<IActionResult> GetPendingPayments()
        {
            try
            {
                var staffId = GetStaffIdFromToken();
                if (staffId == 0)
                    return Unauthorized(new { message = "Invalid staff token" });

                var pendingPayments = await _staffChargingService.GetPendingPaymentsAsync(staffId);
                
                return Ok(new { 
                    message = "Pending payments retrieved successfully", 
                    data = pendingPayments,
                    count = pendingPayments.Count,
                    note = "Use PUT /api/staff/charging/payments/{paymentId}/confirm to confirm payment when customer pays."
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    message = "An error occurred while retrieving pending payments", 
                    error = ex.Message 
                });
            }
        }

        /// <summary>
        /// Xác nhận thanh toán tiền mặt (chuyển từ pending sang success/completed)
        /// PUT /api/staff/charging/payments/{paymentId}/confirm
        /// </summary>
        [HttpPut("payments/{paymentId}/confirm")]
        public async Task<IActionResult> ConfirmCashPayment(int paymentId, [FromBody] UpdatePaymentStatusRequest request)
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

                var result = await _staffChargingService.ConfirmCashPaymentAsync(staffId, paymentId, request);
                if (result == null)
                {
                    return BadRequest(new { 
                        message = "Failed to confirm payment. Payment may not exist, not be in pending status, or you don't have access to this station." 
                    });
                }

                return Ok(new { 
                    message = "Payment confirmed successfully", 
                    data = result 
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    message = "An error occurred while confirming payment", 
                    error = ex.Message 
                });
            }
        }

        /// <summary>
        /// Tạo báo cáo sự cố
        /// POST /api/staff/charging/incidents
        /// </summary>
        [HttpPost("incidents")]
        public async Task<IActionResult> CreateIncidentReport([FromBody] CreateIncidentReportRequest request)
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

                var result = await _staffChargingService.CreateIncidentReportAsync(staffId, request);
                if (result == null)
                {
                    return BadRequest(new { 
                        message = "Failed to create incident report. Charging point may not exist or you don't have access to this station." 
                    });
                }

                return Ok(new { 
                    message = "Incident report created successfully", 
                    data = result 
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    message = "An error occurred while creating incident report", 
                    error = ex.Message 
                });
            }
        }

        /// <summary>
        /// Lấy danh sách báo cáo sự cố tại trạm của staff
        /// GET /api/staff/charging/incidents?status={status}&priority={priority}&page={page}&pageSize={pageSize}
        /// </summary>
        [HttpGet("incidents")]
        public async Task<IActionResult> GetMyStationIncidentReports(
            [FromQuery] string status = "all",
            [FromQuery] string priority = "all",
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var staffId = GetStaffIdFromToken();
                if (staffId == 0)
                    return Unauthorized(new { message = "Invalid staff token" });

                // Validate pagination
                if (page < 1) page = 1;
                if (pageSize < 1 || pageSize > 100) pageSize = 20;

                var reports = await _staffChargingService.GetMyStationIncidentReportsAsync(staffId, status, priority, page, pageSize);
                
                return Ok(new { 
                    message = "Incident reports retrieved successfully", 
                    data = reports,
                    count = reports.Count,
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
                    message = "An error occurred while retrieving incident reports", 
                    error = ex.Message 
                });
            }
        }

        // ========== HELPER METHODS ==========

        /// <summary>
        /// Lấy User ID từ JWT token
        /// Note: Staff không có bảng riêng. Staff là User có role="Staff"
        /// StationStaff.StaffId = User.UserId (foreign key)
        /// </summary>
        private int GetStaffIdFromToken()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                // userId này sẽ được dùng như staffId trong StationStaff.StaffId
                // Vì StationStaff.StaffId là foreign key đến User.UserId
                return userId;
            }
            return 0;
        }
    }
}

