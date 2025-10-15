using EVCharging.BE.Services.Services;
using Microsoft.AspNetCore.Mvc;

namespace EVCharging.BE.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChargingPointsController : ControllerBase
    {
        private readonly IChargingPointService _service;

        public ChargingPointsController(IChargingPointService service)
        {
            _service = service;
        }

        [HttpGet("available")]
        public async Task<IActionResult> GetAvailable()
        {
            var list = await _service.GetAvailableAsync();
            return Ok(list);
        }

        [HttpGet("by-station/{stationId}")]
        public async Task<IActionResult> GetByStation(int stationId)
        {
            var list = await _service.GetByStationAsync(stationId);
            return Ok(list);
        }

        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] string newStatus)
        {
            var result = await _service.UpdateStatusAsync(id, newStatus);
            if (result == null) return NotFound();
            return Ok(result);
        }
    }
}
