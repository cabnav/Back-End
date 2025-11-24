using EVCharging.BE.Common.DTOs.DriverProfiles;
using EVCharging.BE.Services.Services.Users;
using Microsoft.AspNetCore.Mvc;

namespace EVCharging.BE.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DriverProfilesController : ControllerBase
    {
        private readonly IDriverProfileService _svc;
        public DriverProfilesController(IDriverProfileService svc) { _svc = svc; }

        // GET: api/DriverProfiles?page=&pageSize=
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
            => Ok(await _svc.GetAllAsync(page, pageSize));

        // GET: api/DriverProfiles/{id}
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var dto = await _svc.GetByIdAsync(id);
            return dto == null ? NotFound(new { message = "Driver profile not found" }) : Ok(dto);
        }

        // GET: api/DriverProfiles/me
        // Lấy userId từ JWT
        [HttpGet("me")]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> GetMyDriverProfile()
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new { message = "Không thể xác định người dùng. Vui lòng đăng nhập lại." });
            }

            var dto = await _svc.GetByUserIdAsync(userId);
            return dto == null ? NotFound(new { message = "Driver profile not found" }) : Ok(dto);
        }

        // GET: api/DriverProfiles/status
        // Xem trạng thái driver của mình
        [HttpGet("status")]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> GetDriverStatus()
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new { message = "Không thể xác định người dùng. Vui lòng đăng nhập lại." });
            }

            var dto = await _svc.GetByUserIdAsync(userId);
            if (dto == null)
            {
                return NotFound(new { message = "Driver profile not found" });
            }

            return Ok(new
            {
                driverId = dto.DriverId,
                status = dto.Status,
                corporateId = dto.CorporateId,
                createdAt = dto.CreatedAt
            });
        }

        /*// POST: api/DriverProfiles
        [HttpPost]
        public async Task<IActionResult> CreateDriverProfile([FromBody] DriverProfileCreateRequest req)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var dto = await _svc.CreateAsync(req);
            return CreatedAtAction(nameof(GetById), new { id = dto.DriverId }, dto);
        }
        */
        // PUT: api/DriverProfiles/update{id}
        [HttpPut("{id:int}/update")]
        public async Task<IActionResult> UpdateDriverProfile(int id, [FromBody] DriverProfileUpdateRequest req)
        {
            try
            {
                var ok = await _svc.UpdateAsync(id, req);
                return ok ? Ok(new { message = "Updated successfully" })
                          : NotFound(new { message = "Driver profile not found" });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // DELETE: api/DriverProfiles/{id}
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var ok = await _svc.DeleteAsync(id);
            return ok ? Ok(new { message = "Deleted successfully" })
                      : NotFound(new { message = "Driver profile not found" });
        }
    }
}
