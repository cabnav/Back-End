using EVCharging.BE.Common.DTOs.Auth;
using EVCharging.BE.Common.DTOs.Users;
using EVCharging.BE.DAL;
using EVCharging.BE.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace EVCharging.BE.Services.Services.Implementation
{
    public class AuthService : IAuthService
    {
        private readonly EvchargingManagementContext _db;
        private readonly IConfiguration _configuration;
        private readonly IUserService _userService;
        private static readonly HashSet<string> _blacklistedTokens = new();

        public AuthService(EvchargingManagementContext db, IConfiguration configuration, IUserService userService)
        {
            _db = db;
            _configuration = configuration;
            _userService = userService;
        }

        public async Task<AuthResponse?> LoginAsync(LoginRequest request)
        {
            try
            {
                // Find user by email
                var user = await _db.Users
                    .Include(u => u.DriverProfile)
                    .FirstOrDefaultAsync(u => u.Email == request.Email);

                if (user == null)
                    return null;

                // Verify password
                if (!VerifyPassword(request.Password, user.Password))
                    return null;

                // Generate JWT token
                var token = await GenerateTokenAsync(user.UserId, user.Email, user.Role);

                // Create user DTO
                var userDto = new UserDTO
                {
                    UserId = user.UserId,
                    Name = user.Name,
                    Email = user.Email,
                    Phone = user.Phone,
                    Role = user.Role,
                    WalletBalance = user.WalletBalance,
                    BillingType = user.BillingType,
                    MembershipTier = user.MembershipTier,
                    CreatedAt = user.CreatedAt
                };

                return new AuthResponse
                {
                    Token = token,
                    ExpiresAt = DateTime.UtcNow.AddHours(24), // Token expires in 24 hours
                    User = userDto
                };
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<AuthResponse?> RegisterAsync(RegisterRequest request)
        {
            try
            {
                // Check if user already exists
                var existingUser = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
                if (existingUser != null)
                    return null;

                // Create new user
                var user = new User
                {
                    Name = request.Name,
                    Email = request.Email,
                    Password = HashPassword(request.Password),
                    Phone = request.Phone,
                    Role = request.Role,
                    WalletBalance = 0,
                    BillingType = "postpaid",
                    MembershipTier = "basic",
                    CreatedAt = DateTime.UtcNow
                };

                // Add user to database
                await _userService.CreateAsync(user);

                // If role is driver, create driver profile
                if (request.Role == "driver")
                {
                    var driverProfile = new DriverProfile
                    {
                        UserId = user.UserId,
                        LicenseNumber = request.LicenseNumber,
                        VehicleModel = request.VehicleModel,
                        VehiclePlate = request.VehiclePlate,
                        BatteryCapacity = request.BatteryCapacity
                    };

                    _db.DriverProfiles.Add(driverProfile);
                    await _db.SaveChangesAsync();
                }

                // Generate JWT token
                var token = await GenerateTokenAsync(user.UserId, user.Email, user.Role);

                // Create user DTO
                var userDto = new UserDTO
                {
                    UserId = user.UserId,
                    Name = user.Name,
                    Email = user.Email,
                    Phone = user.Phone,
                    Role = user.Role,
                    WalletBalance = user.WalletBalance,
                    BillingType = user.BillingType,
                    MembershipTier = user.MembershipTier,
                    CreatedAt = user.CreatedAt
                };

                return new AuthResponse
                {
                    Token = token,
                    ExpiresAt = DateTime.UtcNow.AddHours(24),
                    User = userDto
                };
            }
            catch (Exception)
            {
                return null;
            }
        }

        public Task<bool> LogoutAsync(string token)
        {
            try
            {
                // Add token to blacklist
                _blacklistedTokens.Add(token);
                return Task.FromResult(true);
            }
            catch (Exception)
            {
                return Task.FromResult(false);
            }
        }

        public Task<bool> ValidateTokenAsync(string token)
        {
            try
            {
                // Check if token is blacklisted
                if (_blacklistedTokens.Contains(token))
                    return Task.FromResult(false);

                var tokenHandler = new JwtSecurityTokenHandler();
                var secret = _configuration["JWT:Secret"];
                if (string.IsNullOrEmpty(secret))
                    return Task.FromResult(false);

                var key = Encoding.UTF8.GetBytes(secret);

                tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = _configuration["JWT:ValidIssuer"],
                    ValidateAudience = true,
                    ValidAudience = _configuration["JWT:ValidAudience"],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                }, out SecurityToken validatedToken);

                return Task.FromResult(true);
            }
            catch (Exception)
            {
                return Task.FromResult(false);
            }
        }

        public Task<string> GenerateTokenAsync(int userId, string email, string role)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var secret = _configuration["JWT:Secret"];
            if (string.IsNullOrEmpty(secret))
                throw new InvalidOperationException("JWT Secret is not configured");

            var key = Encoding.UTF8.GetBytes(secret);

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, userId.ToString()),
                new(ClaimTypes.Email, email),
                new(ClaimTypes.Role, role),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddHours(24),
                Issuer = _configuration["JWT:ValidIssuer"],
                Audience = _configuration["JWT:ValidAudience"],
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return Task.FromResult(tokenHandler.WriteToken(token));
        }

        private string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(hashedBytes);
        }

        private bool VerifyPassword(string password, string hashedPassword)
        {
            var hashedInput = HashPassword(password);
            return hashedInput == hashedPassword;
        }
    }
}
