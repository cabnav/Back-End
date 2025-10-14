using EVCharging.BE.DAL;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using EVCharging.BE.Services.Services.Implementations;
using EVCharging.BE.Services.Services;
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
builder.Services.AddSwaggerGen();

// ------------------------------
// 2️⃣ Configure database
// ------------------------------
builder.Services.AddDbContext<EvchargingManagementContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
);

// ------------------------------
// 3️⃣ Register custom services (DI)
// ------------------------------
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IChargingStationService, ChargingStationService>();
builder.Services.AddScoped<IDriverProfileService, DriverProfileService>(); // ✅ Service DriverProfile

// ------------------------------
// 4️⃣ Configure JWT Authentication
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
    options.RequireHttpsMetadata = false;
    options.TokenValidationParameters = new TokenValidationParameters()
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidAudience = builder.Configuration["JWT:ValidAudience"],
        ValidIssuer = builder.Configuration["JWT:ValidIssuer"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["JWT:Secret"])
        ),
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
    };
});

builder.Services.AddAuthorization();

// ------------------------------
// 5️⃣ Build app
// ------------------------------
var app = builder.Build();

// ------------------------------
// 6️⃣ Seed sample data (auto create test records)
// ------------------------------
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<EvchargingManagementContext>();
    db.Database.EnsureDeleted();  // ❌ Xóa database cũ
    db.Database.EnsureCreated();  // ✅ Tạo lại database mới
    DataSeeder.Seed(db);          // Chạy lại seed data
}

// ------------------------------
// 7️⃣ Configure middleware
// ------------------------------
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
