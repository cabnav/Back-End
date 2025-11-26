# 🔌 Giải Pháp: Validate Connector Type Compatibility

## ❌ Vấn Đề Hiện Tại

**Code hiện tại cho phép sạc khi:**
- Cổng sạc của xe (ví dụ: Type2) ≠ Cổng sạc của điểm sạc (ví dụ: CCS2)
- Không có validation connector type compatibility
- `DriverProfile` không có field `ConnectorType`

---

## ✅ CÁC HƯỚNG XỬ LÝ

### **HƯỚNG 1: Thêm ConnectorType vào DriverProfile (Khuyến nghị) ⭐**

#### **Bước 1: Thêm field vào DriverProfile Entity**

```csharp
// EVCharging.BE.DAL/Entities/DriverProfile.cs
public partial class DriverProfile
{
    // ... existing fields ...
    
    public string? ConnectorType { get; set; } // ✅ Thêm field mới
}
```

#### **Bước 2: Tạo Migration SQL**

```sql
-- Migrations/AddConnectorTypeToDriverProfile.sql
ALTER TABLE DriverProfile
ADD connector_type NVARCHAR(50) NULL;

-- Update existing records nếu có thể infer từ VehicleModel
-- Ví dụ: Tesla thường dùng CCS2, Nissan Leaf dùng CHAdeMO
UPDATE DriverProfile
SET connector_type = 'CCS2'
WHERE VehicleModel LIKE '%Tesla%';

UPDATE DriverProfile
SET connector_type = 'CHAdeMO'
WHERE VehicleModel LIKE '%Nissan%' OR VehicleModel LIKE '%Leaf%';
```

#### **Bước 3: Update DTOs**

```csharp
// EVCharging.BE.Common/DTOs/Users/DriverProfileDTO.cs
public class DriverProfileDTO
{
    // ... existing fields ...
    public string? ConnectorType { get; set; } // ✅ Thêm
}

// EVCharging.BE.Common/DTOs/Auth/RegisterRequest.cs
public class RegisterRequest
{
    // ... existing fields ...
    public string? ConnectorType { get; set; } // ✅ Thêm (optional khi register)
}
```

#### **Bước 4: Tạo Compatibility Service**

```csharp
// EVCharging.BE.Services/Services/Charging/IConnectorCompatibilityService.cs
public interface IConnectorCompatibilityService
{
    /// <summary>
    /// Kiểm tra connector type của xe có tương thích với connector type của điểm sạc không
    /// </summary>
    bool IsCompatible(string? vehicleConnectorType, string? pointConnectorType);
    
    /// <summary>
    /// Lấy danh sách connector types tương thích với connector type của xe
    /// </summary>
    List<string> GetCompatibleConnectorTypes(string? vehicleConnectorType);
}

// EVCharging.BE.Services/Services/Charging/Implementations/ConnectorCompatibilityService.cs
public class ConnectorCompatibilityService : IConnectorCompatibilityService
{
    // Mapping compatibility: Vehicle Connector → Compatible Point Connectors
    private static readonly Dictionary<string, List<string>> CompatibilityMap = new()
    {
        // Type2 (AC) - tương thích với AC và một số DC
        { "Type2", new List<string> { "Type2", "AC" } },
        
        // CCS2 - tương thích với CCS2, CCS1
        { "CCS2", new List<string> { "CCS2", "CCS" } },
        { "CCS1", new List<string> { "CCS1", "CCS" } },
        { "CCS", new List<string> { "CCS", "CCS1", "CCS2" } },
        
        // CHAdeMO - chỉ tương thích với CHAdeMO
        { "CHAdeMO", new List<string> { "CHAdeMO" } },
        
        // Type1 (AC) - tương thích với Type1, AC
        { "Type1", new List<string> { "Type1", "AC" } },
        
        // AC generic - tương thích với Type1, Type2, AC
        { "AC", new List<string> { "AC", "Type1", "Type2" } }
    };

    public bool IsCompatible(string? vehicleConnectorType, string? pointConnectorType)
    {
        // Nếu không có thông tin, cho phép (backward compatibility)
        if (string.IsNullOrWhiteSpace(vehicleConnectorType) || 
            string.IsNullOrWhiteSpace(pointConnectorType))
        {
            // ⚠️ Có thể return false nếu muốn strict validation
            return true; // Hoặc false tùy business requirement
        }

        // Normalize: chuyển về uppercase để so sánh
        var vehicleType = vehicleConnectorType.Trim().ToUpperInvariant();
        var pointType = pointConnectorType.Trim().ToUpperInvariant();

        // Exact match
        if (vehicleType == pointType)
            return true;

        // Check compatibility map
        if (CompatibilityMap.TryGetValue(vehicleType, out var compatibleTypes))
        {
            return compatibleTypes.Any(ct => 
                ct.Equals(pointType, StringComparison.OrdinalIgnoreCase));
        }

        // Default: không tương thích
        return false;
    }

    public List<string> GetCompatibleConnectorTypes(string? vehicleConnectorType)
    {
        if (string.IsNullOrWhiteSpace(vehicleConnectorType))
            return new List<string>(); // Hoặc return all types nếu muốn

        var vehicleType = vehicleConnectorType.Trim().ToUpperInvariant();
        
        if (CompatibilityMap.TryGetValue(vehicleType, out var compatibleTypes))
        {
            return compatibleTypes.ToList();
        }

        return new List<string> { vehicleConnectorType }; // Chỉ chính nó
    }
}
```

#### **Bước 5: Validate trong ChargingService**

```csharp
// EVCharging.BE.Services/Services/Charging/Implementations/ChargingService.cs

private readonly IConnectorCompatibilityService _compatibilityService;

public ChargingService(
    EvchargingManagementContext db,
    ICostCalculationService costCalculationService,
    ISessionMonitorService sessionMonitorService,
    INotificationService notificationService,
    IConnectorCompatibilityService compatibilityService) // ✅ Thêm
{
    _db = db;
    _costCalculationService = costCalculationService;
    _sessionMonitorService = sessionMonitorService;
    _notificationService = notificationService;
    _compatibilityService = compatibilityService; // ✅
}

public async Task<ChargingSessionResponse?> StartSessionAsync(ChargingSessionStartRequest request)
{
    // ... existing code ...
    
    // ✅ THÊM VALIDATION CONNECTOR TYPE
    var chargingPoint = await _db.ChargingPoints
        .Include(cp => cp.Station)
        .FirstOrDefaultAsync(cp => cp.PointId == chargingPointId);

    var driver = await _db.DriverProfiles
        .Include(d => d.User)
        .FirstOrDefaultAsync(d => d.DriverId == request.DriverId);

    if (chargingPoint == null || driver == null)
    {
        return null;
    }

    // ✅ Validate connector compatibility
    if (!_compatibilityService.IsCompatible(driver.ConnectorType, chargingPoint.ConnectorType))
    {
        Console.WriteLine($"⚠️ [StartSessionAsync] Connector mismatch - Vehicle: {driver.ConnectorType}, Point: {chargingPoint.ConnectorType}");
        throw new InvalidOperationException(
            $"Cổng sạc của xe ({driver.ConnectorType ?? "chưa cấu hình"}) không tương thích với cổng sạc của điểm sạc ({chargingPoint.ConnectorType ?? "N/A"}). " +
            $"Vui lòng chọn điểm sạc có cổng {driver.ConnectorType} hoặc cập nhật thông tin xe của bạn.");
    }
    
    // ... continue with session creation ...
}
```

#### **Bước 6: Validate trong Controller**

```csharp
// EVCharging.BE.API/Controllers/ChargingSessionsController.cs

private readonly IConnectorCompatibilityService _compatibilityService;

public ChargingSessionsController(
    IChargingService chargingService,
    ISessionMonitorService sessionMonitorService,
    ISignalRNotificationService signalRService,
    EvchargingManagementContext db,
    IConnectorCompatibilityService compatibilityService) // ✅ Thêm
{
    _chargingService = chargingService;
    _sessionMonitorService = sessionMonitorService;
    _signalRService = signalRService;
    _db = db;
    _compatibilityService = compatibilityService; // ✅
}

[HttpPost("start")]
public async Task<IActionResult> StartSession([FromBody] WalkInSessionStartRequest request)
{
    // ... existing validation ...
    
    // ✅ Lấy charging point và driver profile
    var chargingPoint = await _db.ChargingPoints
        .FirstOrDefaultAsync(p => 
            (!string.IsNullOrEmpty(request.PointQrCode) && p.QrCode == request.PointQrCode) ||
            (request.ChargingPointId.HasValue && p.PointId == request.ChargingPointId.Value));
    
    if (chargingPoint == null)
    {
        return NotFound(new { message = "Không tìm thấy điểm sạc." });
    }

    // ✅ Validate connector compatibility
    if (!_compatibilityService.IsCompatible(driverProfile.ConnectorType, chargingPoint.ConnectorType))
    {
        var compatibleTypes = _compatibilityService.GetCompatibleConnectorTypes(driverProfile.ConnectorType);
        return BadRequest(new
        {
            message = $"Cổng sạc của xe ({driverProfile.ConnectorType ?? "chưa cấu hình"}) không tương thích với cổng sạc của điểm sạc ({chargingPoint.ConnectorType ?? "N/A"}).",
            vehicleConnectorType = driverProfile.ConnectorType,
            pointConnectorType = chargingPoint.ConnectorType,
            compatibleConnectorTypes = compatibleTypes,
            suggestion = $"Vui lòng chọn điểm sạc có cổng: {string.Join(", ", compatibleTypes)}"
        });
    }
    
    // ... continue with session start ...
}
```

#### **Bước 7: Đăng ký Service trong Program.cs**

```csharp
// EVCharging.BE.API/Program.cs

// Charging
builder.Services.AddScoped<IChargingService, ChargingService>();
builder.Services.AddScoped<IConnectorCompatibilityService, ConnectorCompatibilityService>(); // ✅ Thêm
```

---

### **HƯỚNG 2: Validate dựa trên VehicleModel (Tạm thời, không khuyến nghị)**

Nếu không muốn thêm field mới, có thể infer connector type từ VehicleModel:

```csharp
public class ConnectorTypeInferenceService
{
    private static readonly Dictionary<string, string> VehicleModelToConnectorMap = new()
    {
        { "Tesla", "CCS2" },
        { "Nissan Leaf", "CHAdeMO" },
        { "BMW i3", "CCS2" },
        // ... more mappings
    };

    public string? InferConnectorType(string? vehicleModel)
    {
        if (string.IsNullOrWhiteSpace(vehicleModel))
            return null;

        foreach (var kvp in VehicleModelToConnectorMap)
        {
            if (vehicleModel.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                return kvp.Value;
        }

        return null; // Unknown
    }
}
```

**⚠️ Nhược điểm:**
- Không chính xác (nhiều model có nhiều connector options)
- Khó maintain
- Không linh hoạt

---

### **HƯỚNG 3: Validate ở Reservation (Bổ sung)**

Cũng cần validate khi tạo reservation:

```csharp
// EVCharging.BE.Services/Services/Reservations/Implementations/ReservationService.cs

public async Task<ReservationResponse?> CreateReservationAsync(CreateReservationRequest request)
{
    // ... existing code ...
    
    // ✅ Validate connector compatibility
    var point = await _db.ChargingPoints
        .FirstOrDefaultAsync(p => p.PointId == request.PointId);
    
    var driver = await _db.DriverProfiles
        .FirstOrDefaultAsync(d => d.DriverId == request.DriverId);
    
    if (point != null && driver != null)
    {
        if (!_compatibilityService.IsCompatible(driver.ConnectorType, point.ConnectorType))
        {
            throw new InvalidOperationException(
                $"Cổng sạc của xe không tương thích với điểm sạc đã chọn.");
        }
    }
    
    // ... continue ...
}
```

---

## 📋 CHECKLIST IMPLEMENTATION

### **Phase 1: Database & Entity**
- [ ] Thêm field `ConnectorType` vào `DriverProfile` entity
- [ ] Tạo migration SQL script
- [ ] Update `EvchargingManagementContext` mapping (nếu cần)

### **Phase 2: DTOs & Requests**
- [ ] Thêm `ConnectorType` vào `DriverProfileDTO`
- [ ] Thêm `ConnectorType` vào `RegisterRequest` (optional)
- [ ] Thêm `ConnectorType` vào update driver profile request

### **Phase 3: Compatibility Service**
- [ ] Tạo `IConnectorCompatibilityService` interface
- [ ] Implement `ConnectorCompatibilityService`
- [ ] Định nghĩa compatibility mapping
- [ ] Đăng ký service trong `Program.cs`

### **Phase 4: Validation Logic**
- [ ] Validate trong `ChargingService.StartSessionAsync`
- [ ] Validate trong `ChargingSessionsController.StartSession`
- [ ] Validate trong `ReservationService.CreateReservationAsync`
- [ ] Validate trong `ReservationsController` (nếu có)

### **Phase 5: User Experience**
- [ ] Update API response với error message rõ ràng
- [ ] Suggest compatible connector types khi lỗi
- [ ] Update frontend để hiển thị connector type requirement
- [ ] Update registration form để collect connector type

### **Phase 6: Testing**
- [ ] Test với compatible connectors (should pass)
- [ ] Test với incompatible connectors (should fail)
- [ ] Test với null/missing connector type (decide behavior)
- [ ] Test backward compatibility với existing data

---

## 🎯 RECOMMENDED APPROACH

**Khuyến nghị: HƯỚNG 1 (Thêm ConnectorType vào DriverProfile)**

**Lý do:**
- ✅ Chính xác nhất
- ✅ Dễ maintain
- ✅ Linh hoạt (user có thể chọn/update)
- ✅ Có thể validate ở nhiều nơi
- ✅ Tốt cho UX (suggest compatible points)

**Implementation Order:**
1. Database migration (thêm field)
2. Update entities & DTOs
3. Tạo compatibility service
4. Add validation vào charging flow
5. Update registration/update forms
6. Testing

---

## 💡 BONUS: Enhanced Features

### **1. Auto-suggest Compatible Points**

```csharp
// API endpoint: GET /api/charging-points/compatible?driverId={id}
public async Task<IActionResult> GetCompatiblePoints(int driverId)
{
    var driver = await _db.DriverProfiles
        .FirstOrDefaultAsync(d => d.DriverId == driverId);
    
    if (driver == null || string.IsNullOrWhiteSpace(driver.ConnectorType))
    {
        return BadRequest(new { message = "Vui lòng cấu hình connector type cho xe của bạn." });
    }
    
    var compatibleTypes = _compatibilityService.GetCompatibleConnectorTypes(driver.ConnectorType);
    
    var points = await _db.ChargingPoints
        .Where(p => compatibleTypes.Contains(p.ConnectorType) && p.Status == "available")
        .Select(p => new { p.PointId, p.ConnectorType, p.Station.Name })
        .ToListAsync();
    
    return Ok(points);
}
```

### **2. Connector Type Validation Helper**

```csharp
public static class ConnectorTypeValidator
{
    public static ValidationResult ValidateCompatibility(
        string? vehicleConnector, 
        string? pointConnector)
    {
        if (string.IsNullOrWhiteSpace(vehicleConnector))
        {
            return ValidationResult.Warning(
                "Chưa cấu hình connector type cho xe. Vui lòng cập nhật thông tin xe.");
        }
        
        if (string.IsNullOrWhiteSpace(pointConnector))
        {
            return ValidationResult.Warning(
                "Điểm sạc chưa có thông tin connector type.");
        }
        
        // ... compatibility check ...
    }
}
```

---

## 📝 NOTES

- **Backward Compatibility:** Quyết định behavior khi `ConnectorType` null:
  - Option 1: Cho phép (warning only)
  - Option 2: Block (strict validation)
  - **Khuyến nghị:** Option 1 cho existing users, Option 2 cho new users

- **Connector Type Values:** Cần standardize:
  - "CCS", "CCS1", "CCS2"
  - "CHAdeMO"
  - "Type1", "Type2"
  - "AC" (generic)

- **Future Enhancement:** Có thể thêm adapter/cable support (một số xe có thể dùng adapter)

---

**Chúc bạn implement thành công! 🚀**

