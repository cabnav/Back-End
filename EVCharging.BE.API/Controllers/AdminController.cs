using EVCharging.BE.Services.Services;
using Microsoft.AspNetCore.Mvc;

namespace EVCharging.BE.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AdminController : ControllerBase
    {
        private readonly IAdminService _service;

        public AdminController(IAdminService service)
        {
            _service = service;
        }

        // ✅ Dashboard tổng quan
        [HttpGet("system-stats")]
        public async Task<IActionResult> GetSystemStats()
        {
            var result = await _service.GetSystemStatsAsync();
            return Ok(result);
        }

        // ✅ Hiệu suất trạm
        [HttpGet("station-performance")]
        public async Task<IActionResult> GetStationPerformance()
        {
            var result = await _service.GetStationPerformanceAsync();
            return Ok(result);
        }

        // ✅ Doanh thu
        [HttpGet("revenue-analytics")]
        public async Task<IActionResult> GetRevenueAnalytics([FromQuery] DateTime? from, [FromQuery] DateTime? to)
        {
            var result = await _service.GetRevenueAnalyticsAsync(from, to);
            return Ok(result);
        }

        // ✅ Mẫu usage pattern (theo giờ & ngày)
        [HttpGet("usage-patterns")]
        public async Task<IActionResult> GetUsagePatterns()
        {
            var result = await _service.GetUsagePatternAsync();
            return Ok(result);
        }

        // ✅ Nhân viên
        [HttpGet("staff-performance")]
        public async Task<IActionResult> GetStaffPerformance()
        {
            var result = await _service.GetStaffPerformanceAsync();
            return Ok(result);
        }
    }
}
