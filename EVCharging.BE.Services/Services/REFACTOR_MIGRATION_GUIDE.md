# 🔄 Services Refactor - Migration Guide

## 📁 **CẤU TRÚC THƯ MỤC MỚI**

```
EVCharging.BE.Services/Services/
├── Auth/
│   ├── IAuthService.cs ✅ (đã move)
│   ├── IPasswordResetService.cs ✅ (đã move)
│   └── Implementations/
│       ├── AuthService.cs (cần move)
│       └── PasswordResetService.cs (cần move)
│
├── Charging/
│   ├── IChargingService.cs (cần move)
│   ├── IChargingPointService.cs (cần move)
│   ├── IChargingStationService.cs (cần move)
│   ├── ISessionMonitorService.cs (cần move)
│   ├── ICostCalculationService.cs (cần move)
│   └── Implementations/
│       ├── ChargingService.cs (cần move)
│       ├── ChargingPointService.cs (cần move)
│       ├── ChargingStationService.cs (cần move)
│       ├── SessionMonitorService.cs (cần move)
│       └── CostCalculationService.cs (cần move)
│
├── Payment/
│   ├── IPaymentService.cs (cần move)
│   ├── IVNPayService.cs (cần move)
│   ├── IMoMoService.cs (cần move)
│   └── Implementations/
│       ├── PaymentService.cs (cần move)
│       ├── VNPayService.cs (cần move)
│       └── MoMoService.cs (cần move)
│
├── User/
│   ├── IUserService.cs (cần move)
│   ├── IDriverProfileService.cs (cần move)
│   ├── ICorporateAccountService.cs (cần move)
│   └── Implementations/
│       ├── UserService.cs (cần move)
│       ├── DriverProfileService.cs (cần move)
│       └── CorporateAccountService.cs (cần move)
│
├── Reservation/
│   ├── IReservationService.cs (cần move)
│   ├── ITimeValidationService.cs (cần move)
│   ├── IQRCodeService.cs (cần move)
│   └── Implementations/
│       ├── ReservationService.cs (cần move)
│       ├── TimeValidationService.cs (cần move)
│       └── QRCodeService.cs (cần move)
│
├── Admin/
│   ├── IAdminService.cs (cần move)
│   └── Implementations/
│       └── AdminService.cs (cần move)
│
├── Notification/
│   ├── IEmailService.cs (cần move)
│   ├── ISignalRNotificationService.cs (cần move)
│   └── Implementations/
│       ├── EmailService.cs (cần move)
│       └── SignalRNotificationService.cs (cần move)
│
└── Common/
    ├── ILocationService.cs (cần move)
    ├── ISearchService.cs (cần move)
    └── Implementations/
        ├── LocationService.cs (cần move)
        └── SearchService.cs (cần move)
```

## 🎯 **HƯỚNG DẪN DI CHUYỂN**

### **Step 1: Move Interface Files**

#### **Auth Domain** ✅ (Đã hoàn thành)
- ✅ IAuthService.cs → Auth/IAuthService.cs
- ✅ IPasswordResetService.cs → Auth/IPasswordResetService.cs

#### **Charging Domain** (Cần di chuyển)
```
IChargingService.cs → Charging/IChargingService.cs
IChargingPointService.cs → Charging/IChargingPointService.cs
IChargingStationService.cs → Charging/IChargingStationService.cs
ISessionMonitorService.cs → Charging/ISessionMonitorService.cs
ICostCalculationService.cs → Charging/ICostCalculationService.cs
```

#### **Payment Domain** (Cần di chuyển)
```
IPaymentService.cs → Payment/IPaymentService.cs
IVNPayService.cs → Payment/IVNPayService.cs
IMoMoService.cs → Payment/IMoMoService.cs
```

#### **User Domain** (Cần di chuyển)
```
IUserService.cs → User/IUserService.cs
IDriverProfileService.cs → User/IDriverProfileService.cs
ICorporateAccountService.cs → User/ICorporateAccountService.cs
```

#### **Reservation Domain** (Cần di chuyển)
```
IReservationService.cs → Reservation/IReservationService.cs
ITimeValidationService.cs → Reservation/ITimeValidationService.cs
IQRCodeService.cs → Reservation/IQRCodeService.cs
```

#### **Admin Domain** (Cần di chuyển)
```
IAdminService.cs → Admin/IAdminService.cs
```

#### **Notification Domain** (Cần di chuyển)
```
IEmailService.cs → Notification/IEmailService.cs
ISignalRNotificationService.cs → Notification/ISignalRNotificationService.cs
```

#### **Common Domain** ✅ (Đã hoàn thành)
```
✅ ILocationService.cs → Common/ILocationService.cs (đã tạo)
✅ ISearchService.cs → Common/ISearchService.cs (đã tạo)
```

### **Step 2: Move Implementation Files**

#### **Auth Domain** (Cần di chuyển)
```
Implementation/AuthService.cs → Auth/Implementations/AuthService.cs
Implementation/PasswordResetService.cs → Auth/Implementations/PasswordResetService.cs
```

#### **Charging Domain** (Cần di chuyển)
```
Implementation/ChargingService.cs → Charging/Implementations/ChargingService.cs
Implementation/ChargingPointService.cs → Charging/Implementations/ChargingPointService.cs
Implementation/ChargingStationService.cs → Charging/Implementations/ChargingStationService.cs
Implementation/SessionMonitorService.cs → Charging/Implementations/SessionMonitorService.cs
Implementation/CostCalculationService.cs → Charging/Implementations/CostCalculationService.cs
```

#### **Payment Domain** (Cần di chuyển)
```
Implementation/PaymentService.cs → Payment/Implementations/PaymentService.cs
Implementation/VNPayService.cs → Payment/Implementations/VNPayService.cs
Implementation/MoMoService.cs → Payment/Implementations/MoMoService.cs
```

#### **User Domain** (Cần di chuyển)
```
Implementation/UserService.cs → User/Implementations/UserService.cs
Implementation/DriverProfileService.cs → User/Implementations/DriverProfileService.cs
Implementation/CorporateAccountService.cs → User/Implementations/CorporateAccountService.cs
```

#### **Reservation Domain** (Cần di chuyển)
```
Implementation/ReservationService.cs → Reservation/Implementations/ReservationService.cs
Implementation/TimeValidationService.cs → Reservation/Implementations/TimeValidationService.cs
Implementation/QRCodeService.cs → Reservation/Implementations/QRCodeService.cs
```

#### **Admin Domain** (Cần di chuyển)
```
Implementation/AdminService.cs → Admin/Implementations/AdminService.cs
```

#### **Notification Domain** (Cần di chuyển)
```
Implementation/EmailService.cs → Notification/Implementations/EmailService.cs
Implementation/SignalRNotificationService.cs → Notification/Implementations/SignalRNotificationService.cs
```

#### **Common Domain** ✅ (Đã hoàn thành)
```
✅ LocationService.cs → Common/Implementations/LocationService.cs (đã move)
✅ SearchService.cs → Common/Implementations/SearchService.cs (đã có)
```

## 🔧 **NAMESPACE UPDATES**

Sau khi di chuyển, cần update namespaces:

### **Auth Domain**
```csharp
// Old
namespace EVCharging.BE.Services.Services

// New
namespace EVCharging.BE.Services.Services.Auth
```

### **Charging Domain**
```csharp
// Old
namespace EVCharging.BE.Services.Services

// New
namespace EVCharging.BE.Services.Services.Charging
```

### **Payment Domain**
```csharp
// Old
namespace EVCharging.BE.Services.Services

// New
namespace EVCharging.BE.Services.Services.Payment
```

### **User Domain**
```csharp
// Old
namespace EVCharging.BE.Services.Services

// New
namespace EVCharging.BE.Services.Services.User
```

### **Reservation Domain**
```csharp
// Old
namespace EVCharging.BE.Services.Services

// New
namespace EVCharging.BE.Services.Services.Reservation
```

### **Admin Domain**
```csharp
// Old
namespace EVCharging.BE.Services.Services

// New
namespace EVCharging.BE.Services.Services.Admin
```

### **Notification Domain**
```csharp
// Old
namespace EVCharging.BE.Services.Services

// New
namespace EVCharging.BE.Services.Services.Notification
```

### **Common Domain**
```csharp
// Old
namespace EVCharging.BE.Services.Services

// New
namespace EVCharging.BE.Services.Services.Common
```

## 📋 **PROGRAM.CS UPDATES**

Sau khi di chuyển, cần update Program.cs:

```csharp
// Old registrations
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IChargingStationService, ChargingStationService>();
// ... etc

// New registrations
builder.Services.AddScoped<EVCharging.BE.Services.Services.Auth.IAuthService, EVCharging.BE.Services.Services.Auth.AuthService>();
builder.Services.AddScoped<EVCharging.BE.Services.Services.User.IUserService, EVCharging.BE.Services.Services.User.UserService>();
builder.Services.AddScoped<EVCharging.BE.Services.Services.Charging.IChargingStationService, EVCharging.BE.Services.Services.Charging.ChargingStationService>();
// ... etc
```

## ✅ **CHECKLIST**

- [ ] Move all interface files to correct domains
- [ ] Move all implementation files to correct domains  
- [ ] Update namespaces in all files
- [ ] Update Program.cs registrations
- [ ] Test compilation
- [ ] Clean up old files
- [ ] Update documentation

## 🎯 **BENEFITS AFTER REFACTOR**

1. **Clear Domain Separation** - Mỗi domain có folder riêng
2. **Better Organization** - Dễ tìm kiếm và quản lý
3. **Scalable Architecture** - Dễ thêm services mới
4. **Team Collaboration** - Có thể work parallel trên các domains
5. **Maintainability** - Dễ maintain và debug

---

**🚀 Ready to start migration!**

