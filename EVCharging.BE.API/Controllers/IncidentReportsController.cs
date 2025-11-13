using EVCharging.BE.Common.DTOs.Shared;
using EVCharging.BE.Services.Services.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EVCharging.BE.API.Controllers
{
    /// <summary>
    /// Controller quản lý báo cáo sự cố (cho Admin)
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "AdminPolicy")]
    public class IncidentReportsController : ControllerBase
    {
        private readonly IAdminService _adminService;

        public IncidentReportsController(IAdminService adminService)
        {
            _adminService = adminService;
        }

        /// <summary>
        /// Lấy danh sách báo cáo sự cố (có filter và pagination)
        /// GET /api/incidentreports?status={status}&priority={priority}&stationId={stationId}&page={page}&pageSize={pageSize}
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetIncidentReports([FromQuery] IncidentReportFilter filter)
        {
            try
            {
                // Validate pagination
                if (filter.Page < 1) filter.Page = 1;
                if (filter.PageSize < 1 || filter.PageSize > 100) filter.PageSize = 20;

                var result = await _adminService.GetIncidentReportsAsync(filter);
                return Ok(new
                {
                    message = "Incident reports retrieved successfully",
                    data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "An error occurred while retrieving incident reports",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Lấy chi tiết báo cáo sự cố
        /// GET /api/incidentreports/{reportId}
        /// </summary>
        [HttpGet("{reportId}")]
        public async Task<IActionResult> GetIncidentReportById(int reportId)
        {
            try
            {
                var report = await _adminService.GetIncidentReportByIdAsync(reportId);
                if (report == null)
                {
                    return NotFound(new
                    {
                        message = "Incident report not found"
                    });
                }

                return Ok(new
                {
                    message = "Incident report retrieved successfully",
                    data = report
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "An error occurred while retrieving incident report",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Cập nhật trạng thái báo cáo sự cố
        /// PUT /api/incidentreports/{reportId}/status
        /// </summary>
        [HttpPut("{reportId}/status")]
        public async Task<IActionResult> UpdateIncidentReportStatus(int reportId, [FromBody] UpdateIncidentStatusRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new
                    {
                        message = "Invalid request data",
                        errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage))
                    });
                }

                var adminId = GetAdminIdFromToken();
                if (adminId == 0)
                    return Unauthorized(new { message = "Invalid admin token" });

                var result = await _adminService.UpdateIncidentReportStatusAsync(reportId, adminId, request);
                if (result == null)
                {
                    return BadRequest(new
                    {
                        message = "Failed to update incident report status. Report may not exist or you don't have permission."
                    });
                }

                return Ok(new
                {
                    message = "Incident report status updated successfully",
                    data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "An error occurred while updating incident report status",
                    error = ex.Message
                });
            }
        }

        // ========== HELPER METHODS ==========

        /// <summary>
        /// Lấy Admin ID từ JWT token
        /// </summary>
        private int GetAdminIdFromToken()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                return userId;
            }
            return 0;
        }
    }
}
