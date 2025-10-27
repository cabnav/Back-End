using EVCharging.BE.API.Hubs;
using EVCharging.BE.DAL;
using EVCharging.BE.Services.Services;
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
            sql.CommandTimeout(30);
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

// Users
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IDriverProfileService, DriverProfileService>();
builder.Services.AddScoped<ICorporateAccountService, CorporateAccountService>();

// Charging
builder.Services.AddScoped<IChargingService, ChargingService>();
builder.Services.AddScoped<IChargingStationService, ChargingStationService>();
builder.Services.AddScoped<IChargingPointService, ChargingPointService>();
builder.Services.AddScoped<ISessionMonitorService, SessionMonitorService>();
builder.Services.AddScoped<ICostCalculationService, CostCalculationService>();
builder.Services.AddScoped<IRealTimeChargingService, RealTimeChargingService>();

// Reservations
builder.Services.AddScoped<IReservationService, ReservationService>();
builder.Services.AddScoped<ITimeValidationService, TimeValidationService>();
builder.Services.AddScoped<IQRCodeService, QRCodeService>();
builder.Services.Configure<ReservationBackgroundOptions>(builder.Configuration.GetSection("ReservationBackground"));
builder.Services.AddHostedService<ReservationExpiryWorker>();
builder.Services.AddHostedService<ReservationReminderWorker>();

// Payments
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IVNPayService, VNPayService>();
builder.Services.AddScoped<IMoMoService, MoMoService>();
builder.Services.AddScoped<IWalletService, WalletService>();
// ➕ MockPay (QR giả lập)
builder.Services.AddScoped<IMockPayService, MockPayService>();

// Notifications
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<ISignalRNotificationService, SignalRNotificationService>();

// Admin
builder.Services.AddScoped<IAdminService, AdminService>();

// Common
builder.Services.AddScoped<ILocationService, LocationService>();
builder.Services.AddScoped<ISearchService, SearchService>();

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
builder.Services.AddAuthorization();

// ------------------------------
// 6) SignalR
// ------------------------------
builder.Services.AddSignalR();

// ------------------------------
// 7) Build app
// ------------------------------
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
<head><meta charset='utf-8'><title>MockPay</title></head>
<body style='font-family:sans-serif;padding:24px'>
  <h2>Nạp ví demo</h2>
  <p>Mã GD: <b>{code}</b> — Số tiền: <b>{amount} VND</b></p>
  <form method='post' action='/api/wallettransactions/topup/mock-callback'>
    <input type='hidden' name='code' value='{code}' />
    <button name='success' value='true'  style='padding:8px 14px'>Thanh toán thành công</button>
    <button name='success' value='false' style='padding:8px 14px;margin-left:8px'>Thanh toán thất bại</button>
  </form>
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
app.UseAuthentication();
app.UseAuthorization();

// Hubs
app.MapHub<ChargingSessionHub>("/chargingHub");

// Controllers
app.MapControllers();

app.Run();
