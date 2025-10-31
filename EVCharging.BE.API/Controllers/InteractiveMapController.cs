using EVCharging.BE.Common.DTOs.Stations;
using EVCharging.BE.Services.Services.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EVCharging.BE.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class InteractiveMapController : ControllerBase
    {
        private readonly IInteractiveMapService _mapService;

        public InteractiveMapController(IInteractiveMapService mapService)
        {
            _mapService = mapService;
        }

        [HttpPost("stations")]
        public async Task<IActionResult> GetInteractiveStations([FromBody] StationFilterDTO filter)
        {
            var result = await _mapService.GetInteractiveStationsAsync(filter);
            return Ok(result);
        }

        [HttpGet("nearby")]
        public async Task<IActionResult> GetNearbyStations(
            [FromQuery] double latitude,
            [FromQuery] double longitude,
            [FromQuery] double radiusKm = 10)
        {
            var result = await _mapService.GetNearbyStationsAsync(latitude, longitude, radiusKm);
            return Ok(result);
        }

        [HttpGet("station/{stationId}/status")]
        public async Task<IActionResult> GetStationStatus(int stationId)
        {
            var status = await _mapService.GetStationStatusAsync(stationId);
            if (status == null)
                return NotFound(new { message = "Station not found" });
            return Ok(status);
        }

        [HttpGet("pricing")]
        public async Task<IActionResult> GetStationsWithPricing(
            [FromQuery] double latitude,
            [FromQuery] double longitude,
            [FromQuery] bool showPeakHours = true)
        {
            var result = await _mapService.GetStationsWithPricingAsync(latitude, longitude, showPeakHours);
            return Ok(result);
        }
    }
}
