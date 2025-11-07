using EVCharging.BE.Services.Services.Charging;
using Microsoft.AspNetCore.Mvc;
using CP = EVCharging.BE.Common.DTOs.Stations;

namespace EVCharging.BE.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChargingPointsController : ControllerBase
    {
        private readonly IChargingPointService _service;
        public ChargingPointsController(IChargingPointService service) => _service = service;

        [HttpGet]
        public async Task<IActionResult> GetAll() => Ok(await _service.GetAllAsync());

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var dto = await _service.GetByIdAsync(id);
            return dto is null ? NotFound() : Ok(dto);
        }

        [HttpGet("available")]
        public async Task<IActionResult> GetAvailable() => Ok(await _service.GetAvailableAsync());

        [HttpGet("by-station/{stationId:int}")]
        public async Task<IActionResult> GetByStation(int stationId)
            => Ok(await _service.GetByStationAsync(stationId));

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CP.ChargingPointCreateRequest req)
        {
            var dto = await _service.CreateAsync(req);
            return CreatedAtAction(nameof(GetById), new { id = dto.PointId }, dto);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] CP.ChargingPointUpdateRequest req)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var ok = await _service.UpdateAsync(id, req);
            return ok ? NoContent() : NotFound();

            try
            {
                if (string.IsNullOrWhiteSpace(newStatus))
                {
                    return BadRequest(new { message = "Status cannot be empty" });
                }

                var result = await _service.UpdateStatusAsync(id, newStatus);
                if (result == null) 
                    return NotFound(new { message = "Charging point not found" });
                
                return Ok(new { message = "Status updated successfully", data = result });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    message = "An error occurred while updating status", 
                    error = ex.Message 
                });
            }
        }

    }
}
