using EVCharging.BE.Common.DTOs.Stations;
using EVCharging.BE.Services.Services.Charging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EVCharging.BE.API.Controllers
{
    /// <summary>
    /// Controller for interactive charging station map functionality
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class InteractiveMapController : ControllerBase
    {
        private readonly IChargingStationService _chargingStationService;

        public InteractiveMapController(IChargingStationService chargingStationService)
        {
            _chargingStationService = chargingStationService;
        }

        /// <summary>
        /// Get interactive charging stations for map display
        /// </summary>
        /// <param name="filter">Filter criteria for stations</param>
        /// <returns>List of interactive station data</returns>
        [HttpPost("stations")]
        public async Task<ActionResult<IEnumerable<InteractiveStationDTO>>> GetInteractiveStations([FromBody] StationFilterDTO filter)
        {
            try
            {
                var stations = await _chargingStationService.GetInteractiveStationsAsync(filter);
                return Ok(stations);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving interactive stations", error = ex.Message });
            }
        }

        /// <summary>
        /// Get nearby charging stations with real-time status
        /// </summary>
        /// <param name="latitude">User's latitude</param>
        /// <param name="longitude">User's longitude</param>
        /// <param name="radiusKm">Search radius in kilometers</param>
        /// <param name="connectorTypes">Filter by connector types (CCS, CHAdeMO, AC)</param>
        /// <returns>List of nearby stations</returns>
        [HttpGet("nearby")]
        public async Task<ActionResult<IEnumerable<InteractiveStationDTO>>> GetNearbyStations(
            [FromQuery] double latitude,
            [FromQuery] double longitude,
            [FromQuery] double radiusKm = 10,
            [FromQuery] string? connectorTypes = null)
        {
            try
            {
                var filter = new StationFilterDTO
                {
                    Latitude = latitude,
                    Longitude = longitude,
                    MaxDistanceKm = radiusKm,
                    ConnectorTypes = ParseConnectorTypes(connectorTypes)
                };

                var stations = await _chargingStationService.GetInteractiveStationsAsync(filter);
                return Ok(stations);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving nearby stations", error = ex.Message });
            }
        }

        /// <summary>
        /// Get real-time status of a specific station
        /// </summary>
        /// <param name="stationId">Station ID</param>
        /// <returns>Real-time station status</returns>
        [HttpGet("station/{stationId}/status")]
        public async Task<ActionResult<object>> GetStationStatus(int stationId)
        {
            try
            {
                var status = await _chargingStationService.GetRealTimeStationStatusAsync(stationId);
                if (status == null)
                    return NotFound(new { message = "Station not found" });

                return Ok(status);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving station status", error = ex.Message });
            }
        }


        /// <summary>
        /// Get stations with time-based pricing information
        /// </summary>
        /// <param name="latitude">User's latitude</param>
        /// <param name="longitude">User's longitude</param>
        /// <param name="showPeakHours">Include peak hour pricinsg</param>
        /// <returns>Stations with pricing information</returns>
        [HttpGet("pricing")]
        public async Task<ActionResult<IEnumerable<InteractiveStationDTO>>> GetStationsWithPricing(
            [FromQuery] double latitude,
            [FromQuery] double longitude,
            [FromQuery] bool showPeakHours = true)
        {
            try
            {
                var filter = new StationFilterDTO
                {
                    Latitude = latitude,
                    Longitude = longitude,
                    MaxDistanceKm = 50 // Default 50km radius
                };

                var stations = await _chargingStationService.GetInteractiveStationsAsync(filter);
                
                // Filter to show only stations with pricing info if requested
                if (showPeakHours)
                {
                    stations = stations.Where(s => s.Pricing.PeakHourPrice > 0);
                }

                return Ok(stations);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving stations with pricing", error = ex.Message });
            }
        }

        /// <summary>
        /// Parse connector types from comma-separated string
        /// </summary>
        private List<EVCharging.BE.Common.Enums.ConnectorType>? ParseConnectorTypes(string? connectorTypes)
        {
            if (string.IsNullOrEmpty(connectorTypes))
                return null;

            var types = new List<EVCharging.BE.Common.Enums.ConnectorType>();
            var typeStrings = connectorTypes.Split(',', StringSplitOptions.RemoveEmptyEntries);

            foreach (var typeString in typeStrings)
            {
                if (Enum.TryParse<EVCharging.BE.Common.Enums.ConnectorType>(typeString.Trim(), true, out var connectorType))
                {
                    types.Add(connectorType);
                }
            }

            return types.Any() ? types : null;
        }
    }
}
