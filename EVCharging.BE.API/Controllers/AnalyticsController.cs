using EVCharging.BE.Services;
using Microsoft.AspNetCore.Mvc;

namespace EVCharging.BE.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AnalyticsController : ControllerBase
    {
        private readonly AnalyticsService _analyticsService;

        public AnalyticsController(AnalyticsService analyticsService)
        {
            _analyticsService = analyticsService;
        }

        [HttpGet("driver/monthly")]
        public async Task<IActionResult> GetDriverMonthlyReport(int driverId, int year, int month)
        {
            try
            {
                var report = await _analyticsService.GetDriverMonthlyReportAsync(driverId, year, month);
                return Ok(report);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "An error occurred while retrieving driver analytics report",
                    error = ex.Message
                });
            }
        }
    }
}
