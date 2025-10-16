using EVCharging.BE.Common.DTOs.DriverProfiles;
using EVCharging.BE.Services.Services;
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

        // GET: api/DriverProfiles/me?userId=1
        // (Sau này lấy userId từ JWT)
        [HttpGet("me")]
        public async Task<IActionResult> GetMyDriverProfile([FromQuery] int userId)
        {
            var dto = await _svc.GetByUserIdAsync(userId);
            return dto == null ? NotFound(new { message = "Driver profile not found" }) : Ok(dto);
        }

        //// POST: api/DriverProfiles
        //[HttpPost]
        //public async Task<IActionResult> CreateDriverProfile([FromBody] DriverProfileCreateRequest req)
        //{
        //    if (!ModelState.IsValid) return BadRequest(ModelState);
        //    var dto = await _svc.CreateAsync(req);
        //    return CreatedAtAction(nameof(GetById), new { id = dto.DriverId }, dto);
        //}

        // PUT: api/DriverProfiles/update{id}
        [HttpPut("{id:int}/update")]
        public async Task<IActionResult> UpdateDriverProfile(int id, [FromBody] DriverProfileUpdateRequest req)
        {
            var ok = await _svc.UpdateAsync(id, req);
            return ok ? Ok(new { message = "Updated successfully" })
                      : NotFound(new { message = "Driver profile not found" });
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
