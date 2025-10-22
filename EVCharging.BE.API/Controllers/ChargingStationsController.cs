using EVCharging.BE.Services.DTOs;
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

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var station = await _service.GetByIdAsync(id);
            if (station == null) return NotFound();
            return Ok(station);
        }


        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] StationSearchDTO filter)
        {
            var result = await _service.SearchStationsAsync(filter);
            return Ok(result);
        }

        [HttpGet("{stationId}/status")]
        public async Task<IActionResult> GetStationStatus(int stationId)
        {
            var status = await _service.GetStationStatusAsync(stationId);
            if (status == null) return NotFound();
            return Ok(status);
        }
        [HttpGet("debug")]
        public async Task<IActionResult> DebugStations()
        {
            var list = await _service.GetAllAsync();
            return Ok(list.Select(s => new { s.StationId, s.Name, s.Latitude, s.Longitude }));
        }

    }
}
