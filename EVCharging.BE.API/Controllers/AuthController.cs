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

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        /// <summary>
        /// User login
        /// </summary>
        /// <param name="request">Login credentials</param>
        /// <returns>Authentication response with token</returns>
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
                {
                    return BadRequest(new { message = "Email and password are required" });
                }

                var result = await _authService.LoginAsync(request);
                if (result == null)
                {
                    return Unauthorized(new { message = "Invalid email or password" });
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred during login", error = ex.Message });
            }
        }

        /// <summary>
        /// User registration
        /// </summary>
        /// <param name="request">Registration details</param>
        /// <returns>Authentication response with token</returns>
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            try
            {
                // Check ModelState for validation errors from Data Annotations
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();
                    return BadRequest(new { message = "Validation failed", errors });
                }

                // Additional basic validation
                if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password) || string.IsNullOrEmpty(request.Name))
                {
                    return BadRequest(new { message = "Name, email, and password are required" });
                }

                // Check email format
                if (!request.Email.Contains("@"))
                {
                    return BadRequest(new { message = "Email must contain @ symbol" });
                }

                // Check password length
                if (request.Password.Length < 6)
                {
                    return BadRequest(new { message = "Password must be at least 6 characters" });
                }

                var result = await _authService.RegisterAsync(request);
                if (result == null)
                {
                    return BadRequest(new { message = "User with this email already exists" });
                }

                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                // Handle validation errors from service
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred during registration", error = ex.Message });
            }
        }

        /// <summary>
        /// User logout
        /// </summary>
        /// <returns>Logout confirmation</returns>
        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            try
            {
                var token = Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();
                if (string.IsNullOrEmpty(token))
                {
                    return BadRequest(new { message = "Token not provided" });
                }

                var result = await _authService.LogoutAsync(token);
                if (!result)
                {
                    return BadRequest(new { message = "Failed to logout" });
                }

                return Ok(new { message = "Successfully logged out" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred during logout", error = ex.Message });
            }
        }

        /// <summary>
        /// Validate token
        /// </summary>
        /// <returns>Token validation result</returns>
        [HttpPost("validate")]
        public async Task<IActionResult> ValidateToken()
        {
            try
            {
                var token = Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();
                if (string.IsNullOrEmpty(token))
                {
                    return BadRequest(new { message = "Token not provided" });
                }

                var isValid = await _authService.ValidateTokenAsync(token);
                if (!isValid)
                {
                    return Unauthorized(new { message = "Invalid or expired token" });
                }

                return Ok(new { message = "Token is valid" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred during token validation", error = ex.Message });
            }
        }

        /// <summary>
        /// Get current user profile
        /// </summary>
        /// <returns>Current user information</returns>
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
                    return Unauthorized(new { message = "User not found" });
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
                return StatusCode(500, new { message = "An error occurred while getting profile", error = ex.Message });
            }
        }
    }
}
