using EVCharging.BE.DAL.Entities;
using Microsoft.AspNetCore.Mvc;
using EVCharging.BE.Services.Services;


namespace EVCharging.BE.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DriverProfilesController : ControllerBase
    {
        private readonly IDriverProfileService _driverProfileService;

        public DriverProfilesController(IDriverProfileService driverProfileService)
        {
            _driverProfileService = driverProfileService;
        }

        // GET: api/driverprofiles
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var profiles = await _driverProfileService.GetAllAsync();
            return Ok(profiles);
        }

        // GET: api/driverprofiles/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var profile = await _driverProfileService.GetByIdAsync(id);
            if (profile == null)
                return NotFound(new { message = "Driver profile not found" });

            return Ok(profile);
        }

        // POST: api/driverprofiles
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] DriverProfile driverProfile)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var created = await _driverProfileService.CreateAsync(driverProfile);
            return CreatedAtAction(nameof(GetById), new { id = created.DriverId }, created);
        }

        // PUT: api/driverprofiles/5
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] DriverProfile driverProfile)
        {
            var success = await _driverProfileService.UpdateAsync(id, driverProfile);
            if (!success)
                return NotFound(new { message = "Driver profile not found" });

            return Ok(new { message = "Updated successfully" });
        }

        // DELETE: api/driverprofiles/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var success = await _driverProfileService.DeleteAsync(id);
            if (!success)
                return NotFound(new { message = "Driver profile not found" });

            return Ok(new { message = "Deleted successfully" });
        }
    }
}
