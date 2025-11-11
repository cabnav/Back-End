using EVCharging.BE.Common.DTOs.Subscriptions;
using EVCharging.BE.Services.Services.Subscriptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EVCharging.BE.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PricingPlansController : ControllerBase
    {
        private readonly IPricingPlanService _pricingPlanService;

        public PricingPlansController(IPricingPlanService pricingPlanService)
        {
            _pricingPlanService = pricingPlanService;
        }

        /// <summary>
        /// Lấy tất cả pricing plans (bao gồm cả active và inactive)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAllPlans()
        {
            try
            {
                var plans = await _pricingPlanService.GetAllPlansAsync(activeOnly: false);
                return Ok(new
                {
                    message = "Lấy danh sách gói thành công",
                    plans
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi hệ thống", error = ex.Message });
            }
        }

        /// <summary>
        /// Lấy pricing plan theo ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetPlanById(int id)
        {
            try
            {
                var plan = await _pricingPlanService.GetPlanByIdAsync(id);
                if (plan == null)
                    return NotFound(new { message = $"Không tìm thấy gói với ID {id}" });

                return Ok(new
                {
                    message = "Lấy thông tin gói thành công",
                    plan
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi hệ thống", error = ex.Message });
            }
        }

        /// <summary>
        /// Tạo pricing plan mới (Admin only - có thể thêm [Authorize(Roles = "admin")])
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> CreatePlan([FromBody] PricingPlanCreateRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new { message = "Dữ liệu không hợp lệ", errors = ModelState });
                }

                var plan = await _pricingPlanService.CreatePlanAsync(request);
                return CreatedAtAction(nameof(GetPlanById), new { id = plan.PlanId }, new
                {
                    message = "Tạo gói thành công",
                    plan
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi hệ thống", error = ex.Message });
            }
        }

        /// <summary>
        /// Cập nhật pricing plan (Admin only)
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> UpdatePlan(int id, [FromBody] PricingPlanUpdateRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new { message = "Dữ liệu không hợp lệ", errors = ModelState });
                }

                var plan = await _pricingPlanService.UpdatePlanAsync(id, request);
                if (plan == null)
                    return NotFound(new { message = $"Không tìm thấy gói với ID {id}" });

                return Ok(new
                {
                    message = "Cập nhật gói thành công",
                    plan
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi hệ thống", error = ex.Message });
            }
        }

        /// <summary>
        /// Deactivate pricing plan (Admin only)
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> DeactivatePlan(int id)
        {
            try
            {
                var result = await _pricingPlanService.DeactivatePlanAsync(id);
                if (!result)
                    return NotFound(new { message = $"Không tìm thấy gói với ID {id}" });

                return Ok(new { message = "Vô hiệu hóa gói thành công" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi hệ thống", error = ex.Message });
            }
        }
    }
}
