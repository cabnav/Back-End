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
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using EVCharging.BE.Services.Services.Users;
using EVCharging.BE.Services.Services.Auth;

namespace EVCharging.BE.Services.Services.Auth.Implementations
{
    public class AuthService : IAuthService
    {
        private readonly EvchargingManagementContext _db;
        private readonly IConfiguration _configuration;
        private readonly IUserService _userService;
        private readonly IEmailOTPService _emailOTPService;
        private static readonly HashSet<string> _blacklistedTokens = new();

        public AuthService(EvchargingManagementContext db, IConfiguration configuration, IUserService userService, IEmailOTPService emailOTPService)
        {
            _db = db;
            _configuration = configuration;
            _userService = userService;
            _emailOTPService = emailOTPService;
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
                // Validate email format
                if (!IsValidEmail(request.Email))
                    throw new InvalidOperationException("Email must contain @ symbol and be in valid format");

                // Validate password length
                if (string.IsNullOrEmpty(request.Password) || request.Password.Length < 6)
                    throw new InvalidOperationException("Password must be at least 6 characters");

                // Validate and verify OTP
                var isOtpValid = await _emailOTPService.VerifyOTPAsync(request.Email, request.OtpCode);
                if (!isOtpValid)
                    throw new InvalidOperationException("Invalid or expired OTP code. Please request a new OTP.");

                // Normalize email (trim and lowercase)
                var normalizedEmail = request.Email.Trim().ToLowerInvariant();
                Console.WriteLine($"[Register] Normalized email: '{normalizedEmail}'");

                // Check if user already exists (case-insensitive comparison)
                // Load all emails and compare normalized versions to handle case/whitespace differences
                var existingEmails = await _db.Users.Select(u => u.Email).ToListAsync();
                Console.WriteLine($"[Register] Found {existingEmails.Count} existing users in database");
                
                var emailExists = existingEmails.Any(e => e.Trim().ToLowerInvariant() == normalizedEmail);
                
                if (emailExists)
                {
                    var foundEmail = existingEmails.First(e => e.Trim().ToLowerInvariant() == normalizedEmail);
                    Console.WriteLine($"[Register] Email conflict! Requested: '{normalizedEmail}', Found in DB: '{foundEmail}' (original: '{request.Email}')");
                    throw new InvalidOperationException($"User with this email already exists (found: {foundEmail})");
                }
                
                Console.WriteLine($"[Register] Email '{normalizedEmail}' is available, proceeding with registration");

                // Create new user (always store normalized email)
                var user = new User
                {
                    Name = request.Name,
                    Email = normalizedEmail,
                    Password = HashPassword(request.Password),
                    Phone = request.Phone,
                    Role = request.Role ?? "driver",
                    WalletBalance = 0,
                    BillingType = "postpaid",
                    MembershipTier = "standard",
                    CreatedAt = DateTime.UtcNow
                };

                // Add user to database
                var createdUser = await _userService.CreateAsync(user);

                // If role is driver, create driver profile
                if (request.Role == "driver")
                {
                    // Validate BatteryCapacity > 0 nếu có value
                    if (request.BatteryCapacity.HasValue && request.BatteryCapacity.Value <= 0)
                        throw new ArgumentException("BatteryCapacity must be greater than 0");

                    var driverProfile = new DriverProfile
                    {
                        UserId = createdUser.UserId,
                        LicenseNumber = request.LicenseNumber,
                        VehicleModel = request.VehicleModel,
                        VehiclePlate = request.VehiclePlate,
                        BatteryCapacity = request.BatteryCapacity,
                        ConnectorType = request.ConnectorType
                    };

                    _db.DriverProfiles.Add(driverProfile);
                    await _db.SaveChangesAsync();
                }

                // Generate JWT token
                var token = await GenerateTokenAsync(createdUser.UserId, createdUser.Email, createdUser.Role);

                // Create user DTO
                var userDto = new UserDTO
                {
                    UserId = createdUser.UserId,
                    Name = createdUser.Name,
                    Email = createdUser.Email,
                    Phone = createdUser.Phone,
                    Role = createdUser.Role,
                    WalletBalance = createdUser.WalletBalance,
                    BillingType = createdUser.BillingType,
                    MembershipTier = createdUser.MembershipTier,
                    CreatedAt = createdUser.CreatedAt
                };

                return new AuthResponse
                {
                    Token = token,
                    ExpiresAt = DateTime.UtcNow.AddHours(24),
                    User = userDto
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RegisterAsync ERROR] {ex.Message}");
                Console.WriteLine($"[RegisterAsync ERROR] Inner Exception: {ex.InnerException?.Message}");
                Console.WriteLine($"[RegisterAsync ERROR] StackTrace: {ex.StackTrace}");
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

        private bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            // Check if email contains @ symbol
            if (!email.Contains("@"))
                return false;

            try
            {
                // Use regex to validate email format
                var emailPattern = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$";
                return Regex.IsMatch(email, emailPattern, RegexOptions.IgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public async Task<AuthResponse?> OAuthLoginOrRegisterAsync(OAuthLoginRequest request)
        {
            try
            {
                // Find existing user by Provider + ProviderId
                var existingUser = await _db.Users
                    .Include(u => u.DriverProfile)
                    .FirstOrDefaultAsync(u => u.Provider == request.Provider && u.ProviderId == request.ProviderId);

                User user;

                if (existingUser != null)
                {
                    // User exists, just login
                    user = existingUser;
                }
                else
                {
                    // Check if email already exists (might be registered with regular account)
                    var emailUser = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
                    if (emailUser != null)
                    {
                        throw new InvalidOperationException($"Email {request.Email} is already registered with a different account");
                    }

                    // Create new user with OAuth provider
                    user = new User
                    {
                        Name = request.Name,
                        Email = request.Email,
                        Password = HashPassword(Guid.NewGuid().ToString()), // Random password for OAuth users
                        Phone = request.Phone,
                        Role = request.Role ?? "driver",
                        WalletBalance = 0,
                        BillingType = "postpaid",
                        MembershipTier = "standard",
                        CreatedAt = DateTime.UtcNow,
                        Provider = request.Provider,
                        ProviderId = request.ProviderId,
                        EmailVerified = true // OAuth providers verify email
                    };

                    // Add user to database
                    _db.Users.Add(user);
                    await _db.SaveChangesAsync();

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
    }
}
