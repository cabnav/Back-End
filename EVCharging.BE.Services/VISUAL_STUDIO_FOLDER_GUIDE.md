# 🔧 Visual Studio Folder Guide

## ❌ **VẤN ĐỀ:**
- Folders được tạo bằng command line không hiển thị trong Visual Studio Solution Explorer
- Visual Studio cần được "told" về cấu trúc mới

## ✅ **GIẢI PHÁP:**

### **Option 1: Tạo folders trong Visual Studio (Recommended)**

1. **Mở Visual Studio**
2. **Right-click** vào `Services` folder trong Solution Explorer
3. **Add → New Folder** cho mỗi domain:
   - Auth
   - Charging  
   - Payment
   - User
   - Reservation
   - Admin
   - Notification
   - Common

4. **Trong mỗi domain folder**, tạo subfolder:
   - Implementations

5. **Drag & Drop** files từ old locations vào new folders

### **Option 2: Refresh Solution**

1. **Close Visual Studio**
2. **Delete** `.vs` folder trong solution root
3. **Reopen** solution
4. **Build** solution để refresh

### **Option 3: Manual .csproj Update**

Update `EVCharging.BE.Services.csproj` để include new folders:

```xml
<ItemGroup>
  <Compile Include="Services\Auth\**\*.cs" />
  <Compile Include="Services\Charging\**\*.cs" />
  <Compile Include="Services\Payment\**\*.cs" />
  <Compile Include="Services\User\**\*.cs" />
  <Compile Include="Services\Reservation\**\*.cs" />
  <Compile Include="Services\Admin\**\*.cs" />
  <Compile Include="Services\Notification\**\*.cs" />
  <Compile Include="Services\Common\**\*.cs" />
</ItemGroup>
```

## 🎯 **RECOMMENDED APPROACH:**

**Sử dụng Visual Studio** để tạo folders và move files:

1. **Tạo folders** trong Solution Explorer
2. **Move files** bằng drag & drop
3. **Update namespaces** trong moved files
4. **Build solution** để test

## 📋 **STEP-BY-STEP:**

### **Step 1: Create Domain Folders**
```
Right-click Services → Add → New Folder
- Auth
- Charging
- Payment
- User
- Reservation
- Admin
- Notification
- Common
```

### **Step 2: Create Implementation Subfolders**
```
Right-click each domain → Add → New Folder
- Implementations
```

### **Step 3: Move Interface Files**
```
Drag & Drop from root Services/ to domain folders:
- IAuthService.cs → Auth/
- IPasswordResetService.cs → Auth/
- IChargingService.cs → Charging/
- etc...
```

### **Step 4: Move Implementation Files**
```
Drag & Drop from Implementation/ to domain/Implementations/:
- AuthService.cs → Auth/Implementations/
- ChargingService.cs → Charging/Implementations/
- etc...
```

### **Step 5: Update Namespaces**
Update namespace trong mỗi file:
```csharp
// Old
namespace EVCharging.BE.Services.Services

// New  
namespace EVCharging.BE.Services.Services.Auth
namespace EVCharging.BE.Services.Services.Charging
// etc...
```

### **Step 6: Update Program.cs**
Update service registrations với new namespaces.

### **Step 7: Build & Test**
Build solution để đảm bảo không có lỗi.

---

**🎉 Sau khi hoàn thành, bạn sẽ có cấu trúc clean và organized!**
