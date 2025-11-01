using EVCharging.BE.Common.DTOs.Shared;
using EVCharging.BE.Common.DTOs.Staff;
using EVCharging.BE.Services.Services.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EVCharging.BE.API.Controllers
{
    /// <summary>
    /// Controller quản lý Staff Assignments (chỉ Admin)
    /// </summary>
    [Route("api/admin/staff")]
    [ApiController]
    [Authorize(Policy = "AdminPolicy")]
    public class AdminStaffManagementController : ControllerBase
    {
        private readonly IAdminStaffService _adminStaffService;

        public AdminStaffManagementController(IAdminStaffService adminStaffService)
        {
            _adminStaffService = adminStaffService;
        }

        /// <summary>
        /// Assign staff vào station
        /// POST /api/admin/staff/assignments
        /// </summary>
        [HttpPost("assignments")]
        public async Task<IActionResult> CreateStaffAssignment([FromBody] StaffAssignmentCreateRequest request)
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

                var result = await _adminStaffService.CreateStaffAssignmentAsync(request);
                if (result == null)
                {
                    return BadRequest(new { message = "Failed to create staff assignment" });
                }

                return CreatedAtAction(
                    nameof(GetStaffAssignment),
                    new { assignmentId = result.AssignmentId },
                    new { message = "Staff assignment created successfully", data = result });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "An error occurred while creating staff assignment",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Update staff assignment
        /// PUT /api/admin/staff/assignments/{assignmentId}
        /// </summary>
        [HttpPut("assignments/{assignmentId}")]
        public async Task<IActionResult> UpdateStaffAssignment(int assignmentId, [FromBody] StaffAssignmentUpdateRequest request)
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

                var result = await _adminStaffService.UpdateStaffAssignmentAsync(assignmentId, request);
                if (result == null)
                {
                    return NotFound(new { message = "Staff assignment not found" });
                }

                return Ok(new { message = "Staff assignment updated successfully", data = result });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "An error occurred while updating staff assignment",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Delete staff assignment
        /// DELETE /api/admin/staff/assignments/{assignmentId}
        /// </summary>
        [HttpDelete("assignments/{assignmentId}")]
        public async Task<IActionResult> DeleteStaffAssignment(int assignmentId)
        {
            try
            {
                var result = await _adminStaffService.DeleteStaffAssignmentAsync(assignmentId);
                if (!result)
                {
                    return NotFound(new { message = "Staff assignment not found" });
                }

                return Ok(new { message = "Staff assignment deleted successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "An error occurred while deleting staff assignment",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Lấy chi tiết staff assignment
        /// GET /api/admin/staff/assignments/{assignmentId}
        /// </summary>
        [HttpGet("assignments/{assignmentId}")]
        public async Task<IActionResult> GetStaffAssignment(int assignmentId)
        {
            try
            {
                var result = await _adminStaffService.GetStaffAssignmentByIdAsync(assignmentId);
                if (result == null)
                {
                    return NotFound(new { message = "Staff assignment not found" });
                }

                return Ok(new { data = result });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "An error occurred while retrieving staff assignment",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Lấy danh sách staff assignments với filter
        /// GET /api/admin/staff/assignments
        /// </summary>
        [HttpGet("assignments")]
        public async Task<IActionResult> GetStaffAssignments([FromQuery] StaffAssignmentFilterRequest filter)
        {
            try
            {
                var result = await _adminStaffService.GetStaffAssignmentsAsync(filter);
                return Ok(new
                {
                    message = "Staff assignments retrieved successfully",
                    data = result.Items,
                    pagination = new
                    {
                        totalCount = result.TotalCount,
                        currentPage = result.Page,
                        pageSize = result.PageSize,
                        totalPages = result.TotalPages
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "An error occurred while retrieving staff assignments",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Lấy danh sách staff tại một station
        /// GET /api/admin/staff/assignments/by-station/{stationId}
        /// </summary>
        [HttpGet("assignments/by-station/{stationId}")]
        public async Task<IActionResult> GetStaffByStation(int stationId, [FromQuery] bool onlyActive = false)
        {
            try
            {
                var result = await _adminStaffService.GetStaffByStationAsync(stationId, onlyActive);
                return Ok(new
                {
                    message = "Staff list retrieved successfully",
                    data = result,
                    count = result.Count
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "An error occurred while retrieving staff by station",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Lấy danh sách stations mà một staff được assign
        /// GET /api/admin/staff/assignments/by-staff/{staffId}
        /// </summary>
        [HttpGet("assignments/by-staff/{staffId}")]
        public async Task<IActionResult> GetStationsByStaff(int staffId, [FromQuery] bool onlyActive = false)
        {
            try
            {
                var result = await _adminStaffService.GetStationsByStaffAsync(staffId, onlyActive);
                return Ok(new
                {
                    message = "Station assignments retrieved successfully",
                    data = result,
                    count = result.Count
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "An error occurred while retrieving stations by staff",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Set user thành staff (update role = "Staff")
        /// POST /api/admin/staff/set-as-staff/{userId}
        /// </summary>
        [HttpPost("set-as-staff/{userId}")]
        public async Task<IActionResult> SetUserAsStaff(int userId)
        {
            try
            {
                var result = await _adminStaffService.SetUserAsStaffAsync(userId);
                if (!result)
                {
                    return BadRequest(new { message = "Failed to set user as staff" });
                }

                return Ok(new
                {
                    message = "User has been set as staff successfully",
                    data = new { userId = userId, role = "staff" }
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "An error occurred while setting user as staff",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Kiểm tra có thể assign staff không (validate conflict)
        /// GET /api/admin/staff/assignments/validate
        /// </summary>
        [HttpGet("assignments/validate")]
        public async Task<IActionResult> ValidateAssignment(
            [FromQuery] int staffId,
            [FromQuery] int stationId,
            [FromQuery] DateTime shiftStart,
            [FromQuery] DateTime shiftEnd,
            [FromQuery] int? excludeAssignmentId = null)
        {
            try
            {
                var canAssign = await _adminStaffService.CanAssignStaffAsync(
                    staffId, stationId, shiftStart, shiftEnd, excludeAssignmentId);

                return Ok(new
                {
                    canAssign = canAssign,
                    message = canAssign
                        ? "Staff can be assigned to this station"
                        : "Cannot assign staff. There is a time conflict with existing assignments."
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "An error occurred while validating assignment",
                    error = ex.Message
                });
            }
        }
    }
}





