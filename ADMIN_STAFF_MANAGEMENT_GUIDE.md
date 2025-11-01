# 🎯 Admin Staff Management Guide
**Hướng dẫn đăng ký và quản lý Staff Assignments**

---

## 📋 Tổng quan

Admin có thể CRUD (Create, Read, Update, Delete) assignments của Staff vào các Stations thông qua các APIs sau:

---

## 🔐 Authentication

Tất cả endpoints yêu cầu:
- **Role**: `Admin`
- **Authorization**: `Bearer {admin_token}`

---

## 📡 API Endpoints

### Base URL: `/api/admin/staff`

---

## 1. ✅ **Assign Staff vào Station** (Create)

### `POST /api/admin/staff/assignments`

**Mục đích**: Gán staff vào một trạm sạc với ca làm việc cụ thể.

**Request Body:**
```json
{
  "staffId": 3,
  "stationId": 1,
  "shiftStart": "2025-10-30T08:00:00Z",
  "shiftEnd": "2025-10-30T17:00:00Z",
  "status": "active",
  "notes": "Ca sáng - Ngày thường"
}
```

**Fields:**
- `staffId` (required): ID của staff user (phải có role = "Staff")
- `stationId` (required): ID của station
- `shiftStart` (required): Thời gian bắt đầu ca làm việc
- `shiftEnd` (required): Thời gian kết thúc ca làm việc (phải > shiftStart)
- `status` (optional): "active" hoặc "inactive" (default: "active")
- `notes` (optional): Ghi chú

**Response:**
```json
{
  "message": "Staff assignment created successfully",
  "data": {
    "assignmentId": 1,
    "staffId": 3,
    "staffName": "John Staff",
    "staffEmail": "staff@example.com",
    "stationId": 1,
    "stationName": "Station A",
    "shiftStart": "2025-10-30T08:00:00Z",
    "shiftEnd": "2025-10-30T17:00:00Z",
    "status": "active",
    "isActive": true
  }
}
```

**Validation Rules:**
- ✅ Staff phải có role = "Staff"
- ✅ ShiftStart < ShiftEnd
- ✅ Không được assign cùng 1 staff vào 2 trạm khác nhau trong cùng thời gian
- ✅ Không được có conflict về thời gian với assignments khác

**Error Cases:**
```json
// Conflict - Staff đã được assign vào trạm khác
{
  "message": "Cannot assign staff. Staff is already assigned to another station during this shift time, or there is a time conflict with existing assignments."
}

// User không phải staff
{
  "message": "User 5 is not a staff member"
}

// Invalid time
{
  "message": "Shift start time must be before shift end time"
}
```

---

## 2. 📝 **Update Assignment**

### `PUT /api/admin/staff/assignments/{assignmentId}`

**Mục đích**: Cập nhật thông tin assignment (shift time, status).

**Request Body:**
```json
{
  "shiftStart": "2025-10-30T09:00:00Z",
  "shiftEnd": "2025-10-30T18:00:00Z",
  "status": "active",
  "notes": "Ca sáng - Đã điều chỉnh giờ"
}
```

**Response:**
```json
{
  "message": "Staff assignment updated successfully",
  "data": {
    "assignmentId": 1,
    "staffId": 3,
    "stationId": 1,
    "shiftStart": "2025-10-30T09:00:00Z",
    "shiftEnd": "2025-10-30T18:00:00Z",
    "status": "active"
  }
}
```

---

## 3. 🗑️ **Delete Assignment**

### `DELETE /api/admin/staff/assignments/{assignmentId}`

**Mục đích**: Xóa assignment (remove staff khỏi trạm).

**Response:**
```json
{
  "message": "Staff assignment deleted successfully"
}
```

---

## 4. 🔍 **Get Assignment Detail**

### `GET /api/admin/staff/assignments/{assignmentId}`

**Response:**
```json
{
  "data": {
    "assignmentId": 1,
    "staffId": 3,
    "staffName": "John Staff",
    "staffEmail": "staff@example.com",
    "stationId": 1,
    "stationName": "Station A",
    "shiftStart": "2025-10-30T08:00:00Z",
    "shiftEnd": "2025-10-30T17:00:00Z",
    "status": "active",
    "isActive": true
  }
}
```

---

## 5. 📋 **Get All Assignments** (với filter)

### `GET /api/admin/staff/assignments`

**Query Parameters:**
```
?staffId=3              // Lọc theo staff
?stationId=1            // Lọc theo station
?status=active          // Lọc theo status (active/inactive/all)
?onlyActiveShifts=true  // Chỉ lấy shifts đang active (trong thời gian làm việc)
?date=2025-10-30        // Lọc theo ngày
?page=1                 // Trang
?pageSize=20            // Số items mỗi trang
```

**Example:**
```http
GET /api/admin/staff/assignments?stationId=1&status=active&onlyActiveShifts=true
```

**Response:**
```json
{
  "message": "Staff assignments retrieved successfully",
  "data": [
    {
      "assignmentId": 1,
      "staffId": 3,
      "staffName": "John Staff",
      "stationId": 1,
      "stationName": "Station A",
      "shiftStart": "2025-10-30T08:00:00Z",
      "shiftEnd": "2025-10-30T17:00:00Z",
      "status": "active"
    }
  ],
  "pagination": {
    "totalCount": 1,
    "currentPage": 1,
    "pageSize": 20,
    "totalPages": 1
  }
}
```

---

## 6. 👥 **Get Staff by Station**

### `GET /api/admin/staff/assignments/by-station/{stationId}`

**Mục đích**: Xem tất cả staff đang làm việc tại một trạm.

**Query Parameters:**
```
?onlyActive=true    // Chỉ lấy staff đang trong ca làm việc
```

**Example:**
```http
GET /api/admin/staff/assignments/by-station/1?onlyActive=true
```

**Response:**
```json
{
  "message": "Staff list retrieved successfully",
  "data": [
    {
      "assignmentId": 1,
      "staffId": 3,
      "staffName": "John Staff",
      "staffEmail": "staff@example.com",
      "stationId": 1,
      "stationName": "Station A",
      "shiftStart": "2025-10-30T08:00:00Z",
      "shiftEnd": "2025-10-30T17:00:00Z",
      "status": "active",
      "isActive": true
    }
  ],
  "count": 1
}
```

---

## 7. 🏢 **Get Stations by Staff**

### `GET /api/admin/staff/assignments/by-staff/{staffId}`

**Mục đích**: Xem tất cả trạm mà một staff được assign.

**Query Parameters:**
```
?onlyActive=true    // Chỉ lấy assignments đang active
```

**Example:**
```http
GET /api/admin/staff/assignments/by-staff/3?onlyActive=true
```

**Response:**
```json
{
  "message": "Station assignments retrieved successfully",
  "data": [
    {
      "assignmentId": 1,
      "staffId": 3,
      "stationId": 1,
      "stationName": "Station A",
      "shiftStart": "2025-10-30T08:00:00Z",
      "shiftEnd": "2025-10-30T17:00:00Z",
      "status": "active"
    }
  ],
  "count": 1
}
```

---

## 8. ✅ **Validate Assignment** (Check conflicts)

### `GET /api/admin/staff/assignments/validate`

**Mục đích**: Kiểm tra xem có thể assign staff không (trước khi create).

**Query Parameters:**
```
?staffId=3
&stationId=1
&shiftStart=2025-10-30T08:00:00Z
&shiftEnd=2025-10-30T17:00:00Z
&excludeAssignmentId=1  // Optional: exclude assignment khi update
```

**Example:**
```http
GET /api/admin/staff/assignments/validate?staffId=3&stationId=1&shiftStart=2025-10-30T08:00:00Z&shiftEnd=2025-10-30T17:00:00Z
```

**Response:**
```json
{
  "canAssign": true,
  "message": "Staff can be assigned to this station"
}
```

**Hoặc nếu conflict:**
```json
{
  "canAssign": false,
  "message": "Cannot assign staff. There is a time conflict with existing assignments."
}
```

---

## 🧪 **Test với Swagger**

### **Step 1: Login as Admin**
```http
POST /api/auth/login
{
  "email": "admin@example.com",
  "password": "admin123"
}
```
→ Copy token từ response

### **Step 2: Tạo Staff User** (nếu chưa có)
```http
POST /api/auth/register
{
  "name": "John Staff",
  "email": "staff@example.com",
  "password": "staff123",
  "phone": "0901234567",
  "role": "Staff"
}
```

### **Step 3: Assign Staff vào Station**
```http
POST /api/admin/staff/assignments
Authorization: Bearer {admin_token}

{
  "staffId": 3,
  "stationId": 1,
  "shiftStart": "2025-10-30T08:00:00Z",
  "shiftEnd": "2025-10-30T17:00:00Z",
  "status": "active"
}
```

### **Step 4: Verify Assignment**
```http
GET /api/admin/staff/assignments/by-station/1?onlyActive=true
Authorization: Bearer {admin_token}
```

### **Step 5: Staff Login và Test**
```http
POST /api/auth/login
{
  "email": "staff@example.com",
  "password": "staff123"
}
```

Sau đó staff có thể:
- `GET /api/staff/charging/my-station` → Sẽ thấy Station A
- `GET /api/staff/charging/sessions` → Xem sessions tại Station A

---

## 🔄 **Workflow Example**

### **Scenario: Assign Staff mới vào trạm**

```
1. Admin login
   POST /api/auth/login
   
2. Validate assignment (optional)
   GET /api/admin/staff/assignments/validate?staffId=3&stationId=1&...
   
3. Create assignment
   POST /api/admin/staff/assignments
   {
     "staffId": 3,
     "stationId": 1,
     "shiftStart": "2025-10-30T08:00:00Z",
     "shiftEnd": "2025-10-30T17:00:00Z"
   }
   
4. Verify
   GET /api/admin/staff/assignments/by-station/1?onlyActive=true
   
5. Staff check-in và bắt đầu làm việc
   (Staff dùng các APIs trong StaffChargingController)
```

---

## ⚠️ **Validation Rules Chi Tiết**

### **1. Time Conflict Detection**

Hệ thống kiểm tra **overlapping shifts**:

```
Existing:  [==============]  08:00 - 17:00
New:              [==============]  12:00 - 21:00
                  ↑ CONFLICT!

Existing:  [==============]  08:00 - 17:00
New:                        [==============]  18:00 - 23:00
                            ↑ OK! No conflict
```

### **2. Staff Role Check**

Chỉ user có `role = "Staff"` mới được assign.

### **3. Shift Time Validation**

- `shiftStart` < `shiftEnd` (bắt buộc)
- Không giới hạn độ dài ca làm việc (có thể 1 giờ, 8 giờ, 24 giờ)

---

## 📊 **Use Cases**

### **Use Case 1: Assign Staff ca sáng**
```json
{
  "staffId": 3,
  "stationId": 1,
  "shiftStart": "2025-10-30T08:00:00Z",
  "shiftEnd": "2025-10-30T16:00:00Z",
  "status": "active",
  "notes": "Ca sáng - Thứ 2 đến Thứ 6"
}
```

### **Use Case 2: Assign Staff ca tối**
```json
{
  "staffId": 4,
  "stationId": 1,
  "shiftStart": "2025-10-30T16:00:00Z",
  "shiftEnd": "2025-10-31T00:00:00Z",
  "status": "active",
  "notes": "Ca tối"
}
```

### **Use Case 3: Deactivate Assignment (tạm thời)**
```json
PUT /api/admin/staff/assignments/1
{
  "shiftStart": "2025-10-30T08:00:00Z",
  "shiftEnd": "2025-10-30T17:00:00Z",
  "status": "inactive",  // ← Deactivate
  "notes": "Staff nghỉ phép"
}
```

---

## 🎯 **Kết luận**

Với các APIs này, Admin có thể:
- ✅ Assign staff vào trạm
- ✅ Xem danh sách assignments
- ✅ Update shift time
- ✅ Remove assignments
- ✅ Validate conflicts trước khi assign

**Staff chỉ có thể quản lý phiên sạc tại trạm được assign!** 🔒





