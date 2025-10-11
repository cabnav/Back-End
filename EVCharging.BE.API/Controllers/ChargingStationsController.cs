using EVCharging.BE.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EVCharging.BE.API.Controllers
{
    [ApiController]
    [Route("api/charging-stations")]
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
            if (station == null) return NotFound(new { message = "Charging station not found" });
            return Ok(station);
        }
    }
}
