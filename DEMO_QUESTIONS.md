# 📋 Câu Hỏi Demo Với Giảng Viên - EV Charging Station Management System

## 🎯 1. TỔNG QUAN DỰ ÁN

### Q1: Dự án này giải quyết vấn đề gì?
**Trả lời:**
- Hệ thống quản lý trạm sạc xe điện toàn diện
- Giúp người dùng tìm, đặt chỗ, và sạc xe điện dễ dàng
- Quản lý thanh toán, đăng ký, và theo dõi phiên sạc real-time
- Hỗ trợ nhiều vai trò: Driver, Admin, Staff, Corporate Account

### Q2: Công nghệ stack sử dụng?
**Trả lời:**
- **Backend:** .NET 9.0, ASP.NET Core Web API
- **Frontend:** React/Next.js với Vite
- **Database:** SQL Server với Entity Framework Core 8.0
- **Real-time:** SignalR cho cập nhật phiên sạc live
- **Authentication:** JWT Bearer Token
- **Payment:** Tích hợp VNPay, MoMo, Wallet nội bộ
- **Email:** MailKit cho thông báo email
- **QR Code:** QRCoder cho đặt chỗ và thanh toán

---

## 🏗️ 2. KIẾN TRÚC & THIẾT KẾ

### Q3: Em dùng mô hình hay kiến trúc nào? ⭐ **CÂU HỎI QUAN TRỌNG**
**Trả lời ngắn gọn:**
"Em sử dụng **Layered Architecture (N-Tier Architecture)** kết hợp với **Service Layer Pattern** và một số **Design Patterns** khác."

**Trả lời chi tiết:**
1. **Layered Architecture (Kiến trúc phân lớp):**
   - **Presentation Layer** (`EVCharging.BE.API`): Controllers, xử lý HTTP requests/responses
   - **Business Logic Layer** (`EVCharging.BE.Services`): Tất cả business logic, validation
   - **Data Access Layer** (`EVCharging.BE.DAL`): Entity Framework, Database operations
   - **Common Layer** (`EVCharging.BE.Common`): Shared DTOs, Enums, Constants

2. **Service Layer Pattern:**
   - Mỗi domain có Service interface và Implementation
   - Controller chỉ gọi Service, không gọi Repository trực tiếp
   - Service chứa toàn bộ business logic
   - Ví dụ: `IAuthService`, `IChargingService`, `IPaymentService`

3. **Repository Pattern (Một phần):**
   - Có Repository cho một số entities phức tạp (ví dụ: `DriverProfileRepository`)
   - Đa số services inject DbContext trực tiếp (Entity Framework đã là abstraction layer)

4. **DTO Pattern (Data Transfer Object):**
   - Controller chỉ nhận và trả về DTO, không expose Entity
   - Service convert giữa Entity ↔ DTO
   - Tách biệt internal model và API contract

5. **Dependency Injection Pattern:**
   - Constructor injection cho tất cả dependencies
   - Interface-based design (tất cả services có interface)
   - Đăng ký trong `Program.cs` với lifetime phù hợp (Scoped/Singleton)

6. **Unit of Work Pattern:**
   - DbContext đóng vai trò Unit of Work
   - Một DbContext per request (Scoped lifetime)
   - SaveChanges() ở cuối transaction

**Lưu ý khi trả lời:**
- Nếu thầy hỏi sâu hơn về Clean Architecture: "Em có áp dụng một số nguyên tắc của Clean Architecture như tách biệt layers, DTO pattern, nhưng không hoàn toàn theo Clean Architecture vì vẫn có một số services inject DbContext trực tiếp."
- Nếu thầy hỏi về 3-tier vs n-tier: "Đây là N-Tier Architecture với 4 layers chính, mỗi layer có trách nhiệm rõ ràng và độc lập."

### Q4: Tại sao chọn kiến trúc này?
**Trả lời:**
- **Separation of Concerns:** Mỗi layer có trách nhiệm rõ ràng, dễ maintain
- **Testability:** Có thể mock services để test từng layer độc lập
- **Scalability:** Dễ scale từng layer riêng biệt
- **SOLID Principles:** Tuân thủ Single Responsibility, Dependency Inversion
- **Reusability:** Service layer có thể reuse cho nhiều controllers
- **Team Collaboration:** Nhiều người có thể làm việc trên các layers khác nhau

### Q5: Có tuân thủ Clean Architecture không?
**Trả lời:**
- ✅ **Một số nguyên tắc Clean Architecture:**
  - Controller chỉ nhận DTO, không nhận Entity
  - Service trả về DTO, không trả về Entity
  - Controller không gọi Repository/DbContext trực tiếp
  - Business logic nằm trong Service layer
  - Data access logic tách biệt trong DAL

- ⚠️ **Chưa hoàn toàn Clean Architecture:**
  - Một số services inject DbContext trực tiếp (thay vì Repository)
  - Chưa có Domain layer riêng biệt
  - Chưa có Application layer tách biệt

- **Lý do:** 
  - Entity Framework Core đã là abstraction layer tốt
  - Dự án vừa phải, không cần quá phức tạp
  - Có thể refactor sau nếu cần

---

## 🔐 3. BẢO MẬT & XÁC THỰC

### Q6: Hệ thống xác thực như thế nào?
**Trả lời:**
- **JWT Bearer Token Authentication:**
  - Token có thời hạn, có issuer/audience validation
  - Secret key lưu trong configuration (không hardcode)
- **Email OTP Verification:**
  - OTP 6 chữ số, hết hạn sau 30 phút
  - Single-use OTP (is_used flag)
  - Gửi email HTML đẹp
- **OAuth Integration:**
  - Hỗ trợ Google & Facebook login
  - Auto-register khi lần đầu OAuth login
  - Email tự động verified cho OAuth users

### Q7: Phân quyền (Authorization) được xử lý ra sao?
**Trả lời:**
- **Role-based Authorization:**
  - Roles: Admin, Staff, Driver, Corporate
  - Policy-based: `AdminPolicy`, `StaffPolicy`, `StaffOrAdminPolicy`
  - Case-insensitive role checking
- **Attribute-based:** `[Authorize(Roles = "Admin")]` trên controllers
- **Fine-grained:** Mỗi endpoint có quyền riêng

### Q8: Bảo mật dữ liệu như thế nào?
**Trả lời:**
- **Password Hashing:** SHA256 (không lưu plain text)
- **SQL Injection Protection:** Entity Framework parameterized queries
- **Input Validation:** Validate ở DTO, Service, Controller layers
- **CORS Configuration:** Cấu hình cho phép cross-origin requests
- **No Sensitive Data in Logs:** Không log password, token
- **HTTPS Ready:** Cấu hình sẵn cho production

---

## 💳 4. HỆ THỐNG THANH TOÁN

### Q9: Các phương thức thanh toán hỗ trợ?
**Trả lời:**
- **Wallet (Ví nội bộ):**
  - Nạp tiền qua MockPay, VNPay, MoMo
  - Thanh toán tự động từ ví khi sạc
- **VNPay:** Tích hợp cổng thanh toán VNPay
- **MoMo:** Tích hợp cổng thanh toán MoMo
- **MockPay:** Hệ thống demo để test không cần cổng thật

### Q10: Luồng thanh toán hoạt động như thế nào?
**Trả lời:**
1. User tạo payment request
2. Hệ thống tạo invoice với invoice number
3. Redirect đến cổng thanh toán (hoặc MockPay)
4. Callback từ cổng thanh toán
5. Update payment status và wallet balance
6. Gửi email xác nhận thanh toán

### Q11: Xử lý lỗi thanh toán ra sao?
**Trả lời:**
- Payment status tracking: Pending → Processing → Success/Failed
- Transaction rollback nếu có lỗi
- Retry mechanism cho failed payments
- Email notification cho mọi trạng thái

---

## ⚡ 5. QUẢN LÝ PHIÊN SẠC

### Q12: Luồng bắt đầu phiên sạc?
**Trả lời:**
1. User quét QR code hoặc chọn charging point
2. Kiểm tra reservation (nếu có)
3. Validate charging point available
4. Kiểm tra wallet balance đủ
5. Tạo charging session
6. Start real-time monitoring qua SignalR
7. Tính phí theo thời gian thực

### Q13: Real-time monitoring hoạt động như thế nào?
**Trả lời:**
- **SignalR Hub:** `/chargingHub`
- **Live Updates:**
  - Energy consumed (kWh)
  - Duration
  - Current cost
  - Status changes
- **Background Workers:**
  - `SessionAutoStopWorker` - Tự động dừng khi đầy
  - `ReservationExpiryWorker` - Xử lý reservation hết hạn
  - `ReservationReminderWorker` - Nhắc nhở trước giờ đặt

### Q14: Tính phí như thế nào?
**Trả lời:**
- **Dynamic Pricing:**
  - Theo thời gian (giờ cao điểm/thấp điểm)
  - Theo loại connector (Type 1, Type 2, CCS, CHAdeMO)
  - Theo membership tier
- **Real-time Calculation:** Tính phí theo kWh và thời gian
- **Subscription Discount:** Giảm giá cho user có subscription

---

## 📅 6. HỆ THỐNG ĐẶT CHỖ

### Q15: Reservation system hoạt động ra sao?
**Trả lời:**
- **Time Slot Booking:**
  - Chọn thời gian bắt đầu và kết thúc
  - Validate không conflict với reservation khác
  - QR code generation cho reservation
- **Status Flow:**
  - Pending → Confirmed → Checked In → In Progress → Completed/Cancelled
- **Auto-expiry:** Reservation tự động hủy nếu không check-in đúng giờ
- **Reminder:** Email/SMS nhắc nhở trước giờ đặt

### Q16: Xử lý conflict reservation?
**Trả lời:**
- Validate time slot trước khi tạo reservation
- Check charging point availability
- Prevent double booking
- Auto-cancel nếu user không check-in

---

## 📊 7. PHÂN TÍCH & BÁO CÁO

### Q17: Analytics features có gì?
**Trả lời:**
- **Usage Analytics:**
  - Số phiên sạc theo ngày/tuần/tháng
  - Revenue theo station/user
  - Energy consumption statistics
- **Business Intelligence:**
  - Peak hours analysis
  - Popular stations
  - User behavior patterns
- **Reports:**
  - Invoice generation
  - Payment history
  - Station performance metrics

---

## 🔔 8. THÔNG BÁO

### Q18: Notification system như thế nào?
**Trả lời:**
- **Email Notifications:**
  - OTP verification
  - Payment confirmations
  - Reservation reminders
  - Session updates
  - Invoice emails
- **Real-time Notifications:**
  - SignalR push notifications
  - In-app notifications
- **Notification Types:**
  - Payment, Reservation, Charging, System alerts

---

## 🗄️ 9. DATABASE

### Q19: Database design như thế nào?
**Trả lời:**
- **Entity Framework Core 8.0:**
  - Code-first approach với migrations
  - Relationship mapping (one-to-many, many-to-many)
- **Key Tables:**
  - User, ChargingStation, ChargingPoint, ChargingSession
  - Payment, Invoice, Reservation, Subscription
  - Notification, UsageAnalytic, IncidentReport
- **Cascade Delete:** Xóa user sẽ xóa tất cả dữ liệu liên quan
- **Indexing:** Index trên các trường thường query (email, status)

### Q20: Migration strategy?
**Trả lời:**
- Database-first approach (không dùng EF migrations)
- SQL scripts trong thư mục `Migrations/`
- Manual migration cho production
- Backup trước khi migrate

---

## 🧪 10. TESTING & QUALITY

### Q21: Có viết unit test không?
**Trả lời:**
- Có test files: `AuthTest.http`, `PaymentsTest.http`
- Swagger UI để test API endpoints
- Manual testing với HTTP files
- **Note:** Có thể mở rộng thêm unit tests với xUnit

### Q22: Error handling như thế nào?
**Trả lời:**
- **Try-Catch blocks** trong services
- **Custom exceptions** cho business logic errors
- **HTTP status codes** phù hợp (400, 401, 404, 500)
- **Error messages** rõ ràng, không expose sensitive info
- **Logging** cho debugging (không log sensitive data)

---

## 🚀 11. DEPLOYMENT & SCALABILITY

### Q23: Có sẵn sàng cho production không?
**Trả lời:**
- ✅ Configuration-based (appsettings.json)
- ✅ Environment variables support
- ✅ CORS configuration
- ✅ HTTPS ready
- ✅ Connection string externalized
- ✅ JWT secret từ configuration
- ⚠️ Cần setup email service credentials
- ⚠️ Cần setup payment gateway credentials

### Q24: Có thể scale như thế nào?
**Trả lời:**
- **Horizontal Scaling:**
  - Stateless API (JWT tokens)
  - SignalR có thể scale với Redis backplane
  - Database có thể replicate
- **Caching:** Có thể thêm Redis cache
- **Load Balancing:** API stateless nên dễ load balance
- **Microservices:** Kiến trúc hiện tại dễ tách thành microservices

---

## 📱 12. FRONTEND INTEGRATION

### Q25: Frontend tích hợp như thế nào?
**Trả lời:**
- **REST API:** Tất cả endpoints có Swagger documentation
- **SignalR Client:** Connect đến `/chargingHub` cho real-time
- **JWT Token:** Lưu trong localStorage/sessionStorage
- **CORS:** Đã cấu hình cho phép frontend domain
- **Error Handling:** Standard HTTP status codes

---

## 🎓 13. CÂU HỎI KỸ THUẬT SÂU

### Q26: Tại sao dùng Singleton cho SessionMonitorService?
**Trả lời:**
- SessionMonitorService cần chạy liên tục, không bị dispose khi request scope kết thúc
- Quản lý state của các phiên sạc đang active
- Tránh tạo nhiều instances không cần thiết

### Q27: Background workers hoạt động như thế nào?
**Trả lời:**
- **IHostedService:** Implement background tasks
- **ReservationExpiryWorker:** Check và expire reservations
- **ReservationReminderWorker:** Gửi reminder emails
- **SessionAutoStopWorker:** Tự động dừng session khi đầy
- **ReservationStatusUpdateWorker:** Update status từ checked_in → in_progress

### Q28: Dependency Injection pattern?
**Trả lời:**
- **Constructor Injection:** Tất cả services dùng constructor injection
- **Service Lifetime:**
  - Scoped: Services, Repositories (per request)
  - Singleton: SessionMonitorService (application lifetime)
  - Transient: Không dùng (tránh performance issues)

---

## 💡 14. ĐIỂM MẠNH & ĐIỂM CẦN CẢI THIỆN

### Q29: Điểm mạnh của dự án?
**Trả lời:**
- ✅ Kiến trúc rõ ràng, dễ maintain
- ✅ Security tốt (JWT, OTP, OAuth, password hashing)
- ✅ Real-time features với SignalR
- ✅ Payment integration đầy đủ
- ✅ Clean code, tuân thủ SOLID
- ✅ Comprehensive API documentation (Swagger)
- ✅ Error handling tốt

### Q30: Điểm cần cải thiện?
**Trả lời:**
- ⚠️ Thêm unit tests (xUnit, Moq)
- ⚠️ Thêm integration tests
- ⚠️ Thêm logging framework (Serilog)
- ⚠️ Thêm caching (Redis)
- ⚠️ Thêm rate limiting
- ⚠️ Thêm API versioning
- ⚠️ Thêm health checks
- ⚠️ Thêm monitoring (Application Insights)

---

## 🎯 15. TÓM TẮT DEMO FLOW

### Flow Demo Nên Trình Bày:
1. **Đăng ký/Đăng nhập:**
   - Send OTP → Verify OTP → Register
   - OAuth login (Google/Facebook)
   - JWT token authentication

2. **Tìm trạm sạc:**
   - Search stations
   - Interactive map
   - Filter by connector type, availability

3. **Đặt chỗ:**
   - Create reservation
   - QR code generation
   - Reminder notification

4. **Bắt đầu sạc:**
   - Scan QR code
   - Start charging session
   - Real-time monitoring (SignalR)

5. **Thanh toán:**
   - Nạp ví qua VNPay/MoMo
   - Auto payment từ ví
   - Invoice generation

6. **Quản trị:**
   - Admin dashboard
   - Analytics & reports
   - Staff management

---

## 📝 LƯU Ý KHI DEMO

1. **Chuẩn bị trước:**
   - Test tất cả flows trước khi demo
   - Có data mẫu sẵn
   - Swagger UI mở sẵn

2. **Trình bày:**
   - Bắt đầu từ tổng quan
   - Demo flow chính trước
   - Giải thích technical details khi được hỏi

3. **Xử lý câu hỏi:**
   - Lắng nghe kỹ câu hỏi
   - Trả lời trực tiếp, không lan man
   - Nếu không biết, thừa nhận và hứa tìm hiểu

4. **Highlight:**
   - Security features
   - Real-time capabilities
   - Clean architecture
   - Payment integration

---

**Chúc bạn demo thành công! 🚀**

