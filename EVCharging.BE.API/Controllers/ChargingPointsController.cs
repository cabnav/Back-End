using EVCharging.BE.Services.Services.Charging;
using EVCharging.BE.Common.DTOs.Charging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace EVCharging.BE.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChargingPointsController : ControllerBase
    {
        private readonly IChargingPointService _service;
        private readonly ICostCalculationService _costCalculationService;

        public ChargingPointsController(IChargingPointService service, ICostCalculationService costCalculationService)
        {
            _service = service;
            _costCalculationService = costCalculationService;
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

        /// <summary>
        /// Cập nhật giá sạc - chỉ admin mới được phép
        /// </summary>
        /// <param name="id">ID của charging point</param>
        /// <param name="request">Thông tin cập nhật giá</param>
        /// <returns>Kết quả cập nhật giá</returns>
        [HttpPut("{id}/price")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdatePrice(int id, [FromBody] PriceUpdateRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new { 
                        message = "Invalid request data", 
                        errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)) 
                    });
                }

                // Get current user info
                var currentUser = User.Identity?.Name ?? "Unknown";
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value ?? "User";

                // Double check admin role
                if (userRole != "Admin")
                {
                    return Forbid("Only administrators can update pricing");
                }

                var result = await _costCalculationService.UpdatePricingWithValidationAsync(id, request, currentUser);
                
                return Ok(new { 
                    message = "Price updated successfully",
                    data = result
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    message = "An error occurred while updating price", 
                    error = ex.Message 
                });
            }
        }

        /// <summary>
        /// Lấy thông tin giá hiện tại của charging point
        /// </summary>
        /// <param name="id">ID của charging point</param>
        /// <returns>Thông tin giá hiện tại</returns>
        [HttpGet("{id}/price")]
        public async Task<IActionResult> GetCurrentPrice(int id)
        {
            try
            {
                var currentPrice = await _costCalculationService.GetCurrentPricePerKwhAsync(id);
                
                if (currentPrice == 0)
                {
                    return NotFound(new { message = "Charging point not found or price not set" });
                }

                return Ok(new { 
                    chargingPointId = id,
                    currentPrice = currentPrice,
                    currency = "VND",
                    retrievedAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    message = "An error occurred while retrieving price", 
                    error = ex.Message 
                });
            }
        }
    }
}
