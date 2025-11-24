using EVCharging.BE.Services.Services.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EVCharging.BE.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "AdminPolicy")]
    public class AdminController : ControllerBase
    {
        private readonly IAdminService _service;
        private readonly IDepositService _depositService;

        public AdminController(IAdminService service, IDepositService depositService)
        {
            _service = service;
            _depositService = depositService;
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

        // ✅ Doanh thu theo trạm và phương thức thanh toán
        [HttpGet("revenue-by-station-method")]
        public async Task<IActionResult> GetRevenueByStationAndMethod()
        {
            var result = await _service.GetRevenueByStationAndMethodAsync();
            return Ok(result);
        }

        // ========== STATION ANALYTICS ==========

        /// <summary>
        /// Lấy tần suất sử dụng theo từng trạm
        /// GET /api/admin/stations/{stationId}/usage-frequency?from={date}&to={date}
        /// </summary>
        [HttpGet("stations/{stationId}/usage-frequency")]
        public async Task<IActionResult> GetStationUsageFrequency(
            int stationId,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to)
        {
            try
            {
                var result = await _service.GetStationUsageFrequencyAsync(stationId, from, to);
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Có lỗi xảy ra khi lấy thống kê tần suất sử dụng",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Lấy giờ cao điểm theo từng trạm
        /// GET /api/admin/stations/{stationId}/peak-hours?from={date}&to={date}
        /// </summary>
        [HttpGet("stations/{stationId}/peak-hours")]
        public async Task<IActionResult> GetStationPeakHours(
            int stationId,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to)
        {
            try
            {
                var result = await _service.GetStationPeakHoursAsync(stationId, from, to);
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Có lỗi xảy ra khi lấy thống kê giờ cao điểm",
                    error = ex.Message
                });
            }
        }

        // ========== DEPOSIT MANAGEMENT (Quản lý giá tiền cọc) ==========

        /// <summary>
        /// Lấy giá tiền cọc hiện tại
        /// GET /api/admin/deposit/current
        /// </summary>
        [HttpGet("deposit/current")]
        public async Task<IActionResult> GetCurrentDeposit()
        {
            try
            {
                var depositInfo = await _depositService.GetCurrentDepositInfoAsync();
                var amount = await _depositService.GetCurrentDepositAmountAsync();

                if (depositInfo == null)
                {
                    return Ok(new
                    {
                        message = "Chưa có cấu hình giá tiền cọc, đang sử dụng giá mặc định",
                        amount = amount,
                        isDefault = true
                    });
                }

                return Ok(new
                {
                    message = "Lấy thông tin giá tiền cọc thành công",
                    data = depositInfo
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Có lỗi xảy ra khi lấy thông tin giá tiền cọc",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Cập nhật giá tiền cọc
        /// PUT /api/admin/deposit
        /// </summary>
        [HttpPut("deposit")]
        public async Task<IActionResult> UpdateDeposit([FromBody] UpdateDepositRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new
                    {
                        message = "Dữ liệu không hợp lệ",
                        errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage))
                    });
                }

                if (request.Amount <= 0)
                {
                    return BadRequest(new
                    {
                        message = "Giá tiền cọc phải lớn hơn 0"
                    });
                }

                var result = await _depositService.UpdateDepositAmountAsync(
                    request.Amount,
                    request.Description
                );

                return Ok(new
                {
                    message = "Cập nhật giá tiền cọc thành công",
                    data = result
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new
                {
                    message = ex.Message
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Có lỗi xảy ra khi cập nhật giá tiền cọc",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Lấy lịch sử thay đổi giá tiền cọc
        /// GET /api/admin/deposit/history
        /// </summary>
        [HttpGet("deposit/history")]
        public async Task<IActionResult> GetDepositHistory()
        {
            try
            {
                var history = await _depositService.GetDepositHistoryAsync();
                return Ok(new
                {
                    message = "Lấy lịch sử thay đổi giá tiền cọc thành công",
                    data = history
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Có lỗi xảy ra khi lấy lịch sử thay đổi giá tiền cọc",
                    error = ex.Message
                });
            }
        }
    }

    /// <summary>
    /// Request để cập nhật giá tiền cọc
    /// </summary>
    public class UpdateDepositRequest
    {
        /// <summary>
        /// Giá tiền cọc mới (VNĐ)
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// Mô tả lý do thay đổi (tùy chọn)
        /// </summary>
        public string? Description { get; set; }
    }
}
