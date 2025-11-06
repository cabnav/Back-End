using EVCharging.BE.Common.DTOs.Stations;
using EVCharging.BE.Common.DTOs.Stations.EVCharging.BE.Common.DTOs.Stations;
using EVCharging.BE.Services.Services.Charging;
using Microsoft.AspNetCore.Mvc;

namespace EVCharging.BE.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChargingStationsController : ControllerBase
    {
        private readonly IChargingStationService _service;

        public ChargingStationsController(IChargingStationService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var list = await _service.GetAllAsync();
            return Ok(list);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var s = await _service.GetByIdAsync(id);
            if (s == null)
                return NotFound(new { message = $"Station ID {id} not found" });
            return Ok(s);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] StationCreateRequest req)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var created = await _service.CreateAsync(req);
            return CreatedAtAction(nameof(GetById), new { id = created.StationId }, created);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] StationUpdateRequest req)
        {
            var updated = await _service.UpdateAsync(id, req);
            if (updated == null)
                return NotFound(new { message = $"Station ID {id} not found" });
            return Ok(updated);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var ok = await _service.DeleteAsync(id);
            if (!ok)
                return NotFound(new { message = $"Station ID {id} not found" });
            return NoContent();
        }

        [HttpPost("search")]
        public async Task<IActionResult> Search([FromBody] StationFilterDTO filter)
        {
            var stations = await _service.SearchStationsAsync(filter);
            return Ok(stations);
        }

        [HttpGet("{id:int}/realtime")]
        public async Task<IActionResult> GetRealTimeStatus(int id)
        {
            var status = await _service.GetRealTimeStationStatusAsync(id);
            if (status == null)
                return NotFound(new { message = $"Station ID {id} not found" });
            return Ok(status);
        }
    }
}
