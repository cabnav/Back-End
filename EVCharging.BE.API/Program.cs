using EVCharging.BE.DAL;
using EVCharging.BE.Services.Services;                 // Interfaces + Implementations đều nằm dưới namespace này của bạn
using EVCharging.BE.Services.Services.Implementations;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using EVCharging.BE.Services.Services.Implementations;
using EVCharging.BE.Services.Services;
using EVCharging.BE.API.Services;
using EVCharging.BE.API.Hubs;
using System.Text.Json.Serialization; // ✅ Thêm dòng này

var builder = WebApplication.CreateBuilder(args);

// ------------------------------
// 1️⃣ Add services
// ------------------------------
builder.Services.AddControllers()
    // ✅ Fix lỗi vòng lặp JSON khi serialize (DriverProfile <-> User)
    .AddJsonOptions(opt =>
    {
        opt.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        opt.JsonSerializerOptions.WriteIndented = true;
    });

builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Cấu hình thông tin API
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "EV Charging API",
        Version = "v1"
    });

    // ⚙️ Thêm cấu hình bảo mật cho JWT Bearer
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Nhập token theo định dạng: Bearer {your JWT token}"
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});


    // JWT bearer in Swagger    
    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Enter JWT Bearer token",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
    };

// ---------- Database ----------
builder.Services.AddDbContext<EvchargingManagementContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sql => { sql.EnableRetryOnFailure(3); sql.CommandTimeout(30); }
    )
);

// ---------- DI registrations ----------
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IChargingStationService, ChargingStationService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IChargingService, ChargingService>();
builder.Services.AddScoped<ICostCalculationService, CostCalculationService>();
builder.Services.AddScoped<ISessionMonitorService, SessionMonitorService>();
builder.Services.AddScoped<IDriverProfileService, DriverProfileService>();
builder.Services.AddScoped<ICorporateAccountService, CorporateAccountService>();

// ---------- AuthN/AuthZ ----------
var jwtSecret = builder.Configuration["JWT:Secret"] ?? "dev-secret-change-me";
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
// 5️⃣ Configure SignalR
// ------------------------------
builder.Services.AddSignalR();
builder.Services.AddScoped<ISignalRNotificationService, SignalRNotificationService>();

// ------------------------------
// 5️⃣ Build app
// ------------------------------
var app = builder.Build();

// ---------- Seed data (chỉ dùng khi demo) ----------

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<EvchargingManagementContext>();
    db.Database.EnsureDeleted();  // ❌ Xóa database cũ
    db.Database.EnsureCreated();  // ✅ Tạo lại database mới
    DataSeeder.Seed(db);          // Chạy lại seed data
}

// ---------- Middlewares ----------
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

// Map SignalR Hub
app.MapHub<ChargingSessionHub>("/chargingHub");

app.MapControllers();
app.Run();
