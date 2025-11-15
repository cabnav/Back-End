using EVCharging.BE.Common.DTOs.Subscriptions;
using EVCharging.BE.Services.Services.Subscriptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EVCharging.BE.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SubscriptionsController : ControllerBase
    {
        private readonly ISubscriptionService _subscriptionService;

        public SubscriptionsController(ISubscriptionService subscriptionService)
        {
            _subscriptionService = subscriptionService;
        }

        /// <summary>
        /// Đăng ký gói subscription (Silver, Gold, Platinum)
        /// </summary>
        [HttpPost("subscribe")]
        public async Task<IActionResult> Subscribe([FromBody] SubscribeRequest request)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                
                // Kiểm tra xem user đã có subscription active chưa
                var existingSubscription = await _subscriptionService.GetActiveSubscriptionAsync(userId);
                if (existingSubscription != null && !string.IsNullOrEmpty(existingSubscription.Tier))
                {
                    return BadRequest(new 
                    { 
                        message = $"Bạn đã đăng ký gói {existingSubscription.Tier.ToUpper()}. Vui lòng hủy gói hiện tại trước khi đăng ký gói mới." 
                    });
                }
                
                var result = await _subscriptionService.SubscribeAsync(userId, request);
                
                // Nếu có PaymentUrl (thanh toán MoMo), trả về message khác
                if (!string.IsNullOrEmpty(result.PaymentUrl))
                {
                    return Ok(new
                    {
                        message = $"Vui lòng thanh toán để hoàn tất đăng ký gói {request.Tier.ToUpper()}",
                        subscription = result,
                        paymentUrl = result.PaymentUrl,
                        requiresPayment = true
                    });
                }
                
                return Ok(new
                {
                    message = $"Đăng ký gói {request.Tier.ToUpper()} thành công",
                    subscription = result,
                    requiresPayment = false
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (NotImplementedException ex)
            {
                return StatusCode(501, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi hệ thống", error = ex.Message });
            }
        }

        /// <summary>
        /// Lấy subscription đang active của user
        /// </summary>
        [HttpGet("my-subscription")]
        public async Task<IActionResult> GetMySubscription()
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var subscription = await _subscriptionService.GetActiveSubscriptionAsync(userId);
                
                if (subscription == null)
                {
                    return Ok(new
                    {
                        message = "Bạn chưa đăng ký gói nào",
                        subscription = (object?)null
                    });
                }

                return Ok(new
                {
                    message = "Lấy thông tin gói thành công",
                    subscription
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi hệ thống", error = ex.Message });
            }
        }

        /// <summary>
        /// Hủy subscription
        /// </summary>
        [HttpPost("cancel")]
        public async Task<IActionResult> CancelSubscription()
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var result = await _subscriptionService.CancelSubscriptionAsync(userId);
                
                if (result)
                {
                    return Ok(new { message = "Hủy gói thành công" });
                }

                return BadRequest(new { message = "Không thể hủy gói" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi hệ thống", error = ex.Message });
            }
        }

        /// <summary>
        /// Lấy discount rate hiện tại của user
        /// </summary>
        [HttpGet("discount-rate")]
        public async Task<IActionResult> GetDiscountRate()
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var discountRate = await _subscriptionService.GetDiscountRateAsync(userId);
                
                return Ok(new
                {
                    discountRate,
                    discountPercentage = discountRate * 100
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi hệ thống", error = ex.Message });
            }
        }
    }
}
