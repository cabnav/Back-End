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

        [HttpPut("{id:int}/price")]
        public async Task<IActionResult> UpdatePrice(int id, [FromBody] decimal price)
        {
            if (price < 0) return BadRequest("Price cannot be negative.");

            try
            {
                var dto = await _service.UpdatePriceAsync(id, price);

                // Nếu Service trả về null (thường là không tìm thấy ID)
                return dto is null ? NotFound() : Ok(dto);
            }
            catch (Exception ex)
            {
                // Bắt bất kỳ lỗi nào xảy ra trong Service (ví dụ: lỗi DB, lỗi logic)

                // 500 Internal Server Error với thông báo tùy chỉnh
                // LƯU Ý: Không nên trả về ex.Message cho môi trường Production.
                return StatusCode(500, new
                {
                    message = "An unexpected error occurred while updating the price.",
                    details = ex.Message
                });
            }
        }
    }
}