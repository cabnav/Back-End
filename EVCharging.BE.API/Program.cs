using EVCharging.BE.API.Hubs;
using EVCharging.BE.DAL;
using EVCharging.BE.Services.Services.Staff;
using EVCharging.BE.Services.Services.Staff.Implementations;
using EVCharging.BE.Services.Services.Admin;
using EVCharging.BE.Services.Services.Admin.Implementations;
using EVCharging.BE.Services.Services.Auth;
using EVCharging.BE.Services.Services.Auth.Implementations;
using EVCharging.BE.Services.Services.Background;
using EVCharging.BE.Services.Services.Charging;
using EVCharging.BE.Services.Services.Charging.Implementations;
using EVCharging.BE.Services.Services.Common;
using EVCharging.BE.Services.Services.Common.Implementations;
using EVCharging.BE.Services.Services.Notification;
using EVCharging.BE.Services.Services.Notification.Implementations;
using EVCharging.BE.Services.Services.Payment;
using EVCharging.BE.Services.Services.Payment.Implementations;
using EVCharging.BE.Services.Services.Reservations;
using EVCharging.BE.Services.Services.Reservations.Implementations;
using EVCharging.BE.Services.Services.Subscriptions;
using EVCharging.BE.Services.Services.Subscriptions.Implementations;
using EVCharging.BE.Services.Services.Users;
using EVCharging.BE.Services.Services.Users.Implementations;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);


// ------------------------------
// 1) Controllers + JSON
// ------------------------------
builder.Services.AddControllers()
    .AddJsonOptions(opt =>
    {
        opt.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        opt.JsonSerializerOptions.WriteIndented = true;
        opt.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

// ------------------------------
// 2) Database (DbContextFactory + scoped DbContext)
// ------------------------------
builder.Services.AddDbContextFactory<EvchargingManagementContext>(options =>
{
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sql =>
        {
            sql.EnableRetryOnFailure(3);
            sql.CommandTimeout(60); // Tăng timeout lên 60 giây để tránh timeout khi query notification
        });
});
builder.Services.AddScoped<EvchargingManagementContext>(provider =>
    provider.GetRequiredService<IDbContextFactory<EvchargingManagementContext>>().CreateDbContext());

// ------------------------------
// 3) Swagger / OpenAPI (Swashbuckle)
// ------------------------------
// ❌ Không dùng AddOpenApi khi đã có Swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "EV Charging API", Version = "v1" });

    // JWT
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Nhập: Bearer {token}"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });

    // Tránh trùng tên schema giữa các class cùng tên khác namespace
    c.CustomSchemaIds(t => t.FullName);

    // Map kiểu đặc biệt nếu có dùng
    c.MapType<DateOnly>(() => new OpenApiSchema { Type = "string", Format = "date" });
    c.MapType<TimeOnly>(() => new OpenApiSchema { Type = "string", Format = "time" });
});

// ------------------------------
// 4) DI registrations
// ------------------------------
// Auth
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IPasswordResetService, PasswordResetService>();
builder.Services.AddScoped<IEmailOTPService, EmailOTPService>();

// Users
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IDriverProfileService, DriverProfileService>();
builder.Services.AddScoped<ICorporateAccountService, CorporateAccountService>();

// Subscriptions
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
builder.Services.AddScoped<IPricingPlanService, PricingPlanService>();

// Charging
builder.Services.AddScoped<IChargingService, ChargingService>();
builder.Services.AddScoped<IChargingStationService, ChargingStationService>();
builder.Services.AddScoped<IChargingPointService, ChargingPointService>();
builder.Services.AddSingleton<ISessionMonitorService, SessionMonitorService>(); // ✅ Singleton để tránh dispose khi request scope kết thúc
builder.Services.AddScoped<ICostCalculationService, CostCalculationService>();
builder.Services.AddScoped<IRealTimeChargingService, RealTimeChargingService>();

// Reservations
builder.Services.AddScoped<IReservationService, ReservationService>();
builder.Services.AddScoped<ITimeValidationService, TimeValidationService>();
builder.Services.AddScoped<IStationSearchService, StationSearchService>();
builder.Services.Configure<ReservationBackgroundOptions>(builder.Configuration.GetSection("ReservationBackground"));
builder.Services.AddHostedService<ReservationExpiryWorker>();
builder.Services.AddHostedService<ReservationReminderWorker>();
builder.Services.AddHostedService<SessionAutoStopWorker>();
builder.Services.AddHostedService<ReservationStatusUpdateWorker>(); // ✅ Update reservation status từ "checked_in" → "in_progress"

// Staff
builder.Services.AddScoped<IStaffChargingService, StaffChargingService>();

// Admin - Staff Management
builder.Services.AddScoped<IAdminStaffService, AdminStaffService>();
// Admin - Deposit Management
builder.Services.AddScoped<IDepositService, DepositService>();

// Payments
builder.Services.AddScoped<IWalletService, WalletService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IInvoiceService, InvoiceService>();
// ➕ MockPay (QR giả lập)
builder.Services.AddScoped<IMockPayService, MockPayService>();
// ➕ MoMo Payment
builder.Services.Configure<EVCharging.BE.Common.DTOs.Payments.MomoOptionModel>(builder.Configuration.GetSection("MomoAPI"));
builder.Services.AddScoped<EVCharging.BE.Services.Services.Payment.IMomoService, EVCharging.BE.Services.Services.Payment.Implementations.MomoService>();

// Notifications
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<ISignalRNotificationService, SignalRNotificationService>();
builder.Services.AddScoped<INotificationService, NotificationService>();

// Admin
builder.Services.AddScoped<IAdminService, AdminService>();

// Common
builder.Services.AddHttpClient<ILocationService, LocationService>();
builder.Services.AddScoped<ISearchService, SearchService>();
builder.Services.AddScoped<IInteractiveMapService, InteractiveMapService>();

// Analytics
builder.Services.AddScoped<EVCharging.BE.Services.AnalyticsService>();



// ------------------------------
// 5) AuthN/AuthZ (JWT)
// ------------------------------
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.SaveToken = true;
    options.RequireHttpsMetadata = false; // bật true ở production
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidIssuer = builder.Configuration["JWT:ValidIssuer"],
        ValidAudience = builder.Configuration["JWT:ValidAudience"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["JWT:Secret"]
                ?? throw new InvalidOperationException("JWT Secret is not configured"))
        ),
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ClockSkew = TimeSpan.FromMinutes(1)
    };
});
builder.Services.AddAuthorization(options =>
{
    // Add case-insensitive role policy for Staff
    options.AddPolicy("StaffPolicy", policy =>
        policy.RequireAssertion(context =>
            context.User.IsInRole("Staff") ||
            context.User.IsInRole("staff") ||
            context.User.IsInRole("STAFF")));

    // Add case-insensitive role policy for Admin
    options.AddPolicy("AdminPolicy", policy =>
        policy.RequireAssertion(context =>
            context.User.IsInRole("Admin") ||
            context.User.IsInRole("admin") ||
            context.User.IsInRole("ADMIN")));
    options.AddPolicy("StaffOrAdminPolicy", policy =>
        policy.RequireAssertion(context =>
            context.User.IsInRole("Staff") ||
            context.User.IsInRole("staff") ||
            context.User.IsInRole("STAFF") ||
            context.User.IsInRole("Admin") ||
            context.User.IsInRole("admin") ||
            context.User.IsInRole("ADMIN")));
});

// ------------------------------
// 6) SignalR
// ------------------------------
builder.Services.AddSignalR();

// ------------------------------
// 7) Build app
// ------------------------------
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});
var app = builder.Build();

// ------------------------------
// 8) Dev-only: Swagger + trang MockPay checkout
// ------------------------------
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();

    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "EVCharging.BE.API v1");
        c.RoutePrefix = "swagger"; // /swagger
    });

    // Trang checkout giả lập để test không cần FE / cổng thật
    app.MapGet("/mockpay/checkout/{code}", async (string code, EvchargingManagementContext db) =>
    {
        var pay = await db.Payments
            .FirstOrDefaultAsync(p => p.InvoiceNumber == code && p.PaymentMethod == "mock");
        if (pay is null) return Results.NotFound("Code not found");

        var amount = pay.Amount.ToString("N0");
        var html = $@"<!doctype html>
<html lang='vi'>
<head>
  <meta charset='utf-8'>
  <meta name='viewport' content='width=device-width, initial-scale=1.0'>
  <title>MockPay - Thanh toán nạp ví</title>
  <style>
    * {{ margin: 0; padding: 0; box-sizing: border-box; }}
    body {{
      font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif;
      background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
      min-height: 100vh;
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 20px;
    }}
    .container {{
      background: white;
      border-radius: 20px;
      box-shadow: 0 20px 60px rgba(0,0,0,0.3);
      max-width: 500px;
      width: 100%;
      padding: 40px;
      animation: fadeIn 0.5s ease-in;
    }}
    @keyframes fadeIn {{
      from {{ opacity: 0; transform: translateY(-20px); }}
      to {{ opacity: 1; transform: translateY(0); }}
    }}
    .header {{
      text-align: center;
      margin-bottom: 30px;
    }}
    .header-icon {{
      font-size: 64px;
      margin-bottom: 15px;
    }}
    .header h1 {{
      color: #333;
      font-size: 28px;
      font-weight: 700;
      margin-bottom: 10px;
    }}
    .header p {{
      color: #666;
      font-size: 14px;
    }}
    .info-card {{
      background: linear-gradient(135deg, #f5f7fa 0%, #c3cfe2 100%);
      border-radius: 15px;
      padding: 25px;
      margin-bottom: 30px;
    }}
    .info-row {{
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 12px 0;
      border-bottom: 1px solid rgba(0,0,0,0.1);
    }}
    .info-row:last-child {{
      border-bottom: none;
    }}
    .info-label {{
      color: #555;
      font-size: 14px;
      font-weight: 500;
    }}
    .info-value {{
      color: #333;
      font-size: 16px;
      font-weight: 700;
    }}
    .amount-value {{
      color: #667eea;
      font-size: 24px;
    }}
    .form-group {{
      margin-top: 30px;
    }}
    .button-group {{
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 15px;
      margin-top: 20px;
    }}
    button {{
      width: 100%;
      padding: 16px 24px;
      border: none;
      border-radius: 12px;
      font-size: 16px;
      font-weight: 600;
      cursor: pointer;
      transition: all 0.3s ease;
      text-transform: uppercase;
      letter-spacing: 0.5px;
    }}
    button[name='success'][value='true'] {{
      background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
      color: white;
      box-shadow: 0 4px 15px rgba(102, 126, 234, 0.4);
    }}
    button[name='success'][value='true']:hover {{
      transform: translateY(-2px);
      box-shadow: 0 6px 20px rgba(102, 126, 234, 0.6);
    }}
    button[name='success'][value='true']:active {{
      transform: translateY(0);
    }}
    button[name='success'][value='false'] {{
      background: transparent;
      color: #999;
      font-size: 14px;
      font-weight: 500;
      text-transform: none;
      letter-spacing: 0;
      padding: 8px 16px;
      width: auto;
      box-shadow: none;
    }}
    button[name='success'][value='false']:hover {{
      color: #666;
      text-decoration: underline;
      transform: none;
    }}
    button[name='success'][value='false']:active {{
      color: #555;
    }}
    .footer {{
      text-align: center;
      margin-top: 25px;
      color: #999;
      font-size: 12px;
    }}
    @media (max-width: 480px) {{
      .container {{
        padding: 30px 20px;
      }}
    }}
  </style>
</head>
<body>
  <div class='container'>
    <div class='header'>
      <div class='header-icon'>💳</div>
      <h1>Thanh toán nạp ví</h1>
      <p>MockPay - Hệ thống thanh toán demo</p>
    </div>
    <div class='info-card'>
      <div class='info-row'>
        <span class='info-label'>Mã giao dịch:</span>
        <span class='info-value'>{code}</span>
      </div>
      <div class='info-row'>
        <span class='info-label'>Số tiền:</span>
        <span class='info-value amount-value'>{amount} VND</span>
      </div>
    </div>
    <form method='post' action='/api/wallettransactions/topup/mock-callback'>
      <input type='hidden' name='code' value='{code}' />
      <div class='form-group'>
        <div class='button-group'>
          <button type='submit' name='success' value='true'>XÁC NHẬN THANH TOÁN</button>
          <button type='submit' name='success' value='false'>Hủy thanh toán</button>
        </div>
      </div>
    </form>
    <div class='footer'>
      <p>Đây là trang thanh toán demo. Vui lòng chọn một trong hai tùy chọn để kiểm thử.</p>
    </div>
  </div>
</body></html>";

        // ✅ cách 1: thêm charset vào header
        return Results.Content(html, "text/html; charset=utf-8");

        // (hoặc) cách 2: chỉ định Encoding
        // return Results.Content(html, "text/html", Encoding.UTF8);
    });
}

// ------------------------------
// 9) Middlewares
// ------------------------------
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();

// Hubs
app.MapHub<ChargingSessionHub>("/chargingHub");

// Controllers
app.MapControllers();

app.Run();
