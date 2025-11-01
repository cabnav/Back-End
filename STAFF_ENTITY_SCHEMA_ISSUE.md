# ⚠️ Vấn đề: StaffId và UserId là 2 ID riêng

## Vấn đề:

Bạn đã chỉ ra rằng:
- `StaffId` (trong bảng `Staff`) là PRIMARY KEY tự động tăng
- `UserId` (trong bảng `User`) là PRIMARY KEY tự động tăng
- **Hai ID này KHÔNG THỂ trùng nhau**

Nhưng code hiện tại đang giả định:
- `StationStaff.StaffId` = `User.UserId` (direct foreign key)

## Schema đúng nên là:

```
User (table)
├── UserId (PK, IDENTITY)
├── Role = "Staff"
└── ...

Staff (table) ← BẢNG NÀY CẦN CÓ
├── StaffId (PK, IDENTITY)  ← Tự động tăng riêng
├── UserId (FK) → User.UserId  ← Reference đến User
└── ... (thông tin bổ sung của staff)

StationStaff (table)
├── AssignmentId (PK, IDENTITY)
├── StaffId (FK) → Staff.StaffId  ← Reference đến Staff, KHÔNG phải User
├── StationId (FK)
└── ...
```

## Cần làm gì:

1. **Tạo Entity `Staff`** với:
   - `StaffId` (PK, auto-increment)
   - `UserId` (FK to User)
   - Các fields khác nếu có

2. **Update `StationStaff` Entity**:
   - `StaffId` → FK đến `Staff.StaffId` (thay vì `User.UserId`)

3. **Update DbContext** để map đúng relationship

4. **Update Services**:
   - `GetStaffIdByUserIdAsync()` → Query từ bảng `Staff` để lấy `StaffId` từ `UserId`
   - Tất cả methods dùng `staffId` cần update

Bạn có muốn tôi implement bảng `Staff` này không?

