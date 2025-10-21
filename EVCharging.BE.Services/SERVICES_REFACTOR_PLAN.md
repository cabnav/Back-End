# 🔄 EVCharging.BE.Services - Refactor Plan

## 📊 **PHÂN TÍCH HIỆN TRẠNG**

### ❌ **VẤN ĐỀ:**
- Tất cả interfaces và implementations nằm chung
- Không có phân nhóm theo domain
- Khó quản lý khi scale up
- Không có separation of concerns

### ✅ **GIẢI PHÁP:**
Tổ chức lại theo **Domain-Driven Design (DDD)** pattern

## 🏗️ **CẤU TRÚC MỚI ĐỀ XUẤT**

```
EVCharging.BE.Services/
├── Services/
│   ├── Auth/
│   │   ├── IAuthService.cs
│   │   ├── IPasswordResetService.cs
│   │   └── Implementations/
│   │       ├── AuthService.cs
│   │       └── PasswordResetService.cs
│   │
│   ├── /Charging
│   │   ├── IChargingService.cs
│   │   ├── IChargingPointService.cs
│   │   ├── IChargingStationService.cs
│   │   ├── ISessionMonitorService.cs
│   │   ├── ICostCalculationService.cs
│   │   └── Implementations/
│   │       ├── ChargingService.cs
│   │       ├── ChargingPointService.cs
│   │       ├── ChargingStationService.cs
│   │       ├── SessionMonitorService.cs
│   │       └── CostCalculationService.cs
│   │
│   ├── Payment/
│   │   ├── IPaymentService.cs
│   │   ├── IVNPayService.cs
│   │   ├── IMoMoService.cs
│   │   └── Implementations/
│   │       ├── PaymentService.cs
│   │       ├── VNPayService.cs
│   │       └── MoMoService.cs
│   │
│   ├── User/
│   │   ├── IUserService.cs
│   │   ├── IDriverProfileService.cs
│   │   ├── ICorporateAccountService.cs
│   │   └── Implementations/
│   │       ├── UserService.cs
│   │       ├── DriverProfileService.cs
│   │       └── CorporateAccountService.cs
│   │
│   ├── Reservation/
│   │   ├── IReservationService.cs
│   │   ├── ITimeValidationService.cs
│   │   ├── IQRCodeService.cs
│   │   └── Implementations/
│   │       ├── ReservationService.cs
│   │       ├── TimeValidationService.cs
│   │       └── QRCodeService.cs
│   │
│   ├── Admin/
│   │   ├── IAdminService.cs
│   │   └── Implementations/
│   │       └── AdminService.cs
│   │
│   ├── Notification/
│   │   ├── IEmailService.cs
│   │   ├── ISignalRNotificationService.cs
│   │   └── Implementations/
│   │       ├── EmailService.cs
│   │       └── SignalRNotificationService.cs
│   │
│   └── Common/
│       ├── ILocationService.cs
│       ├── ISearchService.cs
│       └── Implementations/
│           ├── LocationService.cs
│           └── SearchService.cs
```

## 🎯 **LỢI ÍCH CỦA CẤU TRÚC MỚI**

### 1. **Domain Separation**
- Mỗi domain có folder riêng
- Dễ tìm kiếm và quản lý
- Clear separation of concerns

### 2. **Scalability**
- Dễ thêm services mới
- Dễ maintain và debug
- Team có thể work parallel

### 3. **Maintainability**
- Code organization rõ ràng
- Dễ refactor từng domain
- Dễ test từng module

### 4. **Developer Experience**
- IntelliSense tốt hơn
- Navigation dễ dàng
- Code review hiệu quả

## 🚀 **IMPLEMENTATION PLAN**

### **Phase 1: Create New Structure**
1. Tạo folder structure mới
2. Move files theo domain
3. Update namespaces
4. Update Program.cs registrations

### **Phase 2: Update References**
1. Update using statements
2. Update dependency injections
3. Test compilation
4. Fix any breaking changes

### **Phase 3: Cleanup**
1. Remove old files
2. Update documentation
3. Update test files
4. Final testing

## 📋 **DETAILED MAPPING**

### **Auth Domain**
```
Current → New
IAuthService.cs → Auth/IAuthService.cs
IPasswordResetService.cs → Auth/IPasswordResetService.cs
AuthService.cs → Auth/Implementations/AuthService.cs
PasswordResetService.cs → Auth/Implementations/PasswordResetService.cs
```

### **Charging Domain**
```
Current → New
IChargingService.cs → Charging/IChargingService.cs
IChargingPointService.cs → Charging/IChargingPointService.cs
IChargingStationService.cs → Charging/IChargingStationService.cs
ISessionMonitorService.cs → Charging/ISessionMonitorService.cs
ICostCalculationService.cs → Charging/ICostCalculationService.cs
```

### **Payment Domain**
```
Current → New
IPaymentService.cs → Payment/IPaymentService.cs
IVNPayService.cs → Payment/IVNPayService.cs
IMoMoService.cs → Payment/IMoMoService.cs
PaymentService.cs → Payment/Implementations/PaymentService.cs
VNPayService.cs → Payment/Implementations/VNPayService.cs
MoMoService.cs → Payment/Implementations/MoMoService.cs
```

### **User Domain**
```
Current → New
IUserService.cs → User/IUserService.cs
IDriverProfileService.cs → User/IDriverProfileService.cs
ICorporateAccountService.cs → User/ICorporateAccountService.cs
```

### **Reservation Domain**
```
Current → New
IReservationService.cs → Reservation/IReservationService.cs
ITimeValidationService.cs → Reservation/ITimeValidationService.cs
IQRCodeService.cs → Reservation/IQRCodeService.cs
```

### **Admin Domain**
```
Current → New
IAdminService.cs → Admin/IAdminService.cs
```

### **Notification Domain**
```
Current → New
IEmailService.cs → Notification/IEmailService.cs
ISignalRNotificationService.cs → Notification/ISignalRNotificationService.cs
```

### **Common Domain**
```
Current → New
ILocationService.cs → Common/ILocationService.cs
ISearchService.cs → Common/ISearchService.cs
```

## ⚠️ **RISKS & MITIGATION**

### **Risks:
1. **Breaking changes** trong existing code
2. **Namespace conflicts** 
3. **Dependency injection** issues
4. **Build errors** during transition

### **Mitigation:**
1. **Gradual migration** - move từng domain một
2. **Comprehensive testing** sau mỗi step
3. **Backup current structure** trước khi refactor
4. **Update documentation** cùng lúc

## 🎯 **RECOMMENDATION**

**NÊN REFACTOR** vì:
- ✅ Cải thiện maintainability
- ✅ Dễ scale trong tương lai
- ✅ Better developer experience
- ✅ Clear separation of concerns
- ✅ Easier testing và debugging

**TIMELINE:** 2-3 hours cho full refactor
