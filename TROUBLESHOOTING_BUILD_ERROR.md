# TROUBLESHOOTING: Build Error - File Locked

## 🚨 LỖI

```
Error MSB3027: Could not copy "apphost.exe" to "EVCharging.BE.API.exe"
The file is locked by: "vgc (16204)"
```

## 🔍 NGUYÊN NHÂN

File `EVCharging.BE.API.exe` đang bị lock bởi process khác (có thể là ứng dụng đang chạy, Visual Studio Code, hoặc antivirus).

---

## ✅ CÁCH GIẢI QUYẾT

### **Cách 1: Stop ứng dụng đang chạy (Đơn giản nhất)**

1. **Trong Visual Studio:**
   - Nhấn **Stop** (Shift + F5) để dừng ứng dụng nếu đang chạy
   - Hoặc click vào nút **Stop Debugging** (hình vuông màu đỏ)

2. **Trong Task Manager:**
   - Mở Task Manager (Ctrl + Shift + Esc)
   - Tìm process `EVCharging.BE.API.exe` hoặc `dotnet.exe`
   - Right-click → **End Task**

### **Cách 2: Kill process bằng Command Line**

```powershell
# Tìm và kill process EVCharging.BE.API.exe
taskkill /F /IM EVCharging.BE.API.exe

# Hoặc kill process theo PID (nếu biết PID)
taskkill /F /PID 16204

# Hoặc kill tất cả dotnet processes (cẩn thận!)
taskkill /F /IM dotnet.exe
```

### **Cách 3: Đóng Visual Studio Code (nếu đang mở)**

1. Đóng Visual Studio Code (nếu đang mở)
2. Rebuild project trong Visual Studio

### **Cách 4: Restart Visual Studio**

1. **Save tất cả files**
2. **Close Visual Studio**
3. **Mở lại Visual Studio**
4. **Rebuild project**

### **Cách 5: Xóa bin và obj folders**

```powershell
# Xóa bin và obj folders
Remove-Item -Recurse -Force "EVCharging.BE.API\bin"
Remove-Item -Recurse -Force "EVCharging.BE.API\obj"
```

Sau đó **Rebuild** project.

### **Cách 6: Tắt Antivirus tạm thời (Nếu cần)**

1. Tắt antivirus tạm thời
2. Build lại project
3. Bật lại antivirus

---

## 🎯 GIẢI PHÁP NHANH NHẤT

**Thực hiện theo thứ tự:**

1. ✅ **Stop ứng dụng** trong Visual Studio (Shift + F5)
2. ✅ **Kiểm tra Task Manager** - End Task nếu có process `EVCharging.BE.API.exe`
3. ✅ **Rebuild** project (Right-click project → Rebuild)

---

## 📝 LƯU Ý

- ⚠️ **Không kill process `vgc.exe`** nếu không chắc chắn (có thể là Visual Studio Code)
- ✅ **Luôn Stop ứng dụng** trước khi Build
- ✅ **Kiểm tra Task Manager** nếu lỗi vẫn tiếp tục
- ✅ **Xóa bin/obj** nếu vẫn không được

---

## ✅ SAU KHI GIẢI QUYẾT

1. **Rebuild project:**
   - Right-click project → **Rebuild**
   - Hoặc **Build** → **Rebuild Solution** (Ctrl + Shift + B)

2. **Run lại ứng dụng:**
   - **F5** để Start Debugging
   - Hoặc **Ctrl + F5** để Start Without Debugging

