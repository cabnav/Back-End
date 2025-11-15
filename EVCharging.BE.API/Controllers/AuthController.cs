using EVCharging.BE.Common.DTOs.Auth;
using EVCharging.BE.Services.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EVCharging.BE.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IEmailOTPService _emailOTPService;

        public AuthController(IAuthService authService, IEmailOTPService emailOTPService)
        {
            _authService = authService;
            _emailOTPService = emailOTPService;
        }

        // -------------------- LOGIN --------------------
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Email) ||
                    string.IsNullOrWhiteSpace(request.Password))
                {
                    return BadRequest(new { message = "Email và mật khẩu là bắt buộc" });
                }

                var result = await _authService.LoginAsync(request);

                if (result == null)
                {
                    return Unauthorized(new { message = "Email hoặc mật khẩu không đúng" });
                }

                return Ok(new
                {
                    message = "Đăng nhập thành công!",
                    token = result.Token,
                    expiresAt = result.ExpiresAt,
                    user = new
                    {
                        id = result.User.UserId,
                        email = result.User.Email,
                        name = result.User.Name,
                        role = result.User.Role
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi xảy ra trong quá trình đăng nhập", error = ex.Message });
            }
        }

        // -------------------- REGISTER --------------------
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();

                    return BadRequest(new { message = "Validation failed", errors });
                }

                if (string.IsNullOrWhiteSpace(request.Email) ||
                    string.IsNullOrWhiteSpace(request.Password) ||
                    string.IsNullOrWhiteSpace(request.Name))
                {
                    return BadRequest(new { message = "Tên, email và mật khẩu là bắt buộc" });
                }

                if (!request.Email.Contains("@"))
                {
                    return BadRequest(new { message = "Email phải chứa ký tự @" });
                }

                if (request.Password.Length < 6)
                {
                    return BadRequest(new { message = "Mật khẩu phải ít nhất 6 ký tự" });
                }

                var result = await _authService.RegisterAsync(request);

                if (result == null)
                {
                    return BadRequest(new { message = "Email đã tồn tại" });
                }

                return Ok(new
                {
                    message = "Đăng ký thành công!",
                    token = result.Token,
                    expiresAt = result.ExpiresAt,
                    user = result.User
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi xảy ra trong quá trình đăng ký", error = ex.Message });
            }
        }

        // -------------------- SEND OTP --------------------
        [HttpPost("send-otp")]
        public async Task<IActionResult> SendOTP([FromBody] SendOTPRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();

                    return BadRequest(new { message = "Validation failed", errors });
                }

                var result = await _emailOTPService.SendOTPAsync(request.Email, request.Purpose ?? "registration");

                if (!result)
                {
                    return BadRequest(new { message = "Gửi OTP thất bại. Email có thể đã tồn tại." });
                }

                return Ok(new { message = "OTP đã được gửi đến email của bạn." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi gửi OTP", error = ex.Message });
            }
        }

        // -------------------- VERIFY OTP --------------------
        [HttpPost("verify-otp")]
        public async Task<IActionResult> VerifyOTP([FromBody] VerifyOTPRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();

                    return BadRequest(new { message = "Validation failed", errors });
                }

                var result = await _emailOTPService.VerifyOTPAsync(request.Email, request.OtpCode);

                if (!result)
                {
                    return BadRequest(new { message = "OTP không đúng hoặc đã hết hạn" });
                }

                return Ok(new { message = "Xác thực OTP thành công" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi xác thực OTP", error = ex.Message });
            }
        }

        // -------------------- LOGOUT --------------------
        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            try
            {
                var token = Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();

                if (string.IsNullOrEmpty(token))
                {
                    return BadRequest(new { message = "Không tìm thấy token" });
                }

                var result = await _authService.LogoutAsync(token);

                if (!result)
                {
                    return BadRequest(new { message = "Đăng xuất thất bại" });
                }

                return Ok(new { message = "Đăng xuất thành công" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi đăng xuất", error = ex.Message });
            }
        }

        // -------------------- VALIDATE TOKEN --------------------
        [HttpPost("validate")]
        public async Task<IActionResult> ValidateToken()
        {
            try
            {
                var token = Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();

                if (string.IsNullOrEmpty(token))
                {
                    return BadRequest(new { message = "Không tìm thấy token" });
                }

                var isValid = await _authService.ValidateTokenAsync(token);

                if (!isValid)
                {
                    return Unauthorized(new { message = "Token không hợp lệ hoặc đã hết hạn" });
                }

                return Ok(new { message = "Token hợp lệ" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi kiểm tra token", error = ex.Message });
            }
        }

        // -------------------- OAUTH LOGIN --------------------
        [HttpPost("oauth/login")]
        public async Task<IActionResult> OAuthLogin([FromBody] OAuthLoginRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();

                    return BadRequest(new { message = "Validation failed", errors });
                }

                if (string.IsNullOrEmpty(request.Provider) || string.IsNullOrEmpty(request.ProviderId))
                {
                    return BadRequest(new { message = "Provider và ProviderId là bắt buộc" });
                }

                var result = await _authService.OAuthLoginOrRegisterAsync(request);

                if (result == null)
                {
                    return BadRequest(new { message = "Đăng nhập OAuth thất bại" });
                }

                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi đăng nhập OAuth", error = ex.Message });
            }
        }

        // -------------------- GET CURRENT PROFILE --------------------
        [HttpGet("profile")]
        [Authorize]
        public IActionResult GetProfile()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var email = User.FindFirst(ClaimTypes.Email)?.Value;
                var role = User.FindFirst(ClaimTypes.Role)?.Value;

                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "Không tìm thấy người dùng" });
                }

                return Ok(new
                {
                    UserId = int.Parse(userId),
                    Email = email,
                    Role = role
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi lấy thông tin người dùng", error = ex.Message });
            }
        }
    }
}
