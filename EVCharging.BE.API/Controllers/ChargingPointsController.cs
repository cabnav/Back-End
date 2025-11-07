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
        }

        [HttpPut("{id:int}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] string status)
        {
            var dto = await _service.UpdateStatusAsync(id, status);
            return dto is null ? NotFound() : Ok(dto);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var ok = await _service.DeleteAsync(id);
            return ok ? NoContent() : NotFound();
        }
    }
}