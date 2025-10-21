using EVCharging.BE.Common.DTOs.Auth;
using EVCharging.BE.Services.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EVCharging.BE.API.Controllers
{
    /// <summary>
    /// Controller quản lý đặt lại mật khẩu
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class PasswordResetController : ControllerBase
    {
        private readonly IPasswordResetService _passwordResetService;

        public PasswordResetController(IPasswordResetService passwordResetService)
        {
            _passwordResetService = passwordResetService;
        }

        /// <summary>
        /// Tạo token đặt lại mật khẩu
        /// </summary>
        [HttpPost("request")]
        public async Task<IActionResult> RequestPasswordReset([FromBody] CreatePasswordResetTokenRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(new { message = "Dữ liệu không hợp lệ", errors = ModelState });

                var result = await _passwordResetService.CreatePasswordResetTokenAsync(request);

                if (result.Success)
                {
                    return Ok(result);
                }
                else
                {
                    return BadRequest(result);
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Có lỗi xảy ra khi tạo token đặt lại mật khẩu", error = ex.Message });
            }
        }

        /// <summary>
        /// Đặt lại mật khẩu bằng token
        /// </summary>
        [HttpPost("reset")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(new { message = "Dữ liệu không hợp lệ", errors = ModelState });

                var result = await _passwordResetService.ResetPasswordAsync(request);

                if (result.Success)
                {
                    return Ok(result);
                }
                else
                {
                    return BadRequest(result);
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Có lỗi xảy ra khi đặt lại mật khẩu", error = ex.Message });
            }
        }

        /// <summary>
        /// Validate token đặt lại mật khẩu
        /// </summary>
        [HttpGet("validate/{token}")]
        public async Task<IActionResult> ValidateToken(string token)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(token))
                    return BadRequest(new { message = "Token không được để trống" });
                var isValid = await _passwordResetService.ValidateTokenAsync(token);

                return Ok(new
                {
                    valid = isValid,
                    message = isValid ? "Token hợp lệ" : "Token không hợp lệ hoặc đã hết hạn"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Có lỗi xảy ra khi validate token", error = ex.Message });
            }
        }

        /// <summary>
        /// Lấy thông tin token
        /// </summary>
        [HttpGet("token/{token}")]
        public async Task<IActionResult> GetTokenInfo(string token)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(token))
                    return BadRequest(new { message = "Token không được để trống" });

                var tokenInfo = await _passwordResetService.GetTokenByValueAsync(token);

                if (tokenInfo == null)
                    return NotFound(new { message = "Token không tồn tại" });

                return Ok(tokenInfo);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Có lỗi xảy ra khi lấy thông tin token", error = ex.Message });
            }
        }

        /// <summary>
        /// Revoke token (chỉ dành cho admin)
        /// </summary>
        [HttpPost("revoke/{token}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> RevokeToken(string token)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(token))
                    return BadRequest(new { message = "Token không được để trống" });

                var success = await _passwordResetService.RevokeTokenAsync(token);

                if (success)
                {
                    return Ok(new { message = "Token đã được revoke thành công" });
                }
                else
                {
                    return BadRequest(new { message = "Không thể revoke token" });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Có lỗi xảy ra khi revoke token", error = ex.Message });
            }
        }

        /// <summary>
        /// Lấy tất cả token của user (chỉ dành cho admin)
        /// </summary>
        [HttpGet("user/{userId}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> GetUserTokens(int userId)
        {
            try
            {
                var tokens = await _passwordResetService.GetTokensByUserIdAsync(userId);
                return Ok(tokens);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Có lỗi xảy ra khi lấy danh sách token", error = ex.Message });
            }
        }
    }
}