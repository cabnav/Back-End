# EV Charging Station Management System - Use Cases

## 1. ĐĂNG KÝ VÀ XÁC THỰC NGƯỜI DÙNG

### UC-001: Đăng ký tài khoản người dùng mới
**Mô tả:** Người dùng đăng ký tài khoản mới trong hệ thống
**Actor:** Người dùng mới
**Preconditions:** Chưa có tài khoản trong hệ thống

**Main Flow:**
1. Người dùng truy cập trang đăng ký
2. Nhập thông tin: Email, Mật khẩu, Họ tên, Số điện thoại
3. Hệ thống kiểm tra email chưa tồn tại
4. Hệ thống tạo tài khoản mới với trạng thái "pending"
5. Hệ thống gửi email xác thực
6. Người dùng nhận email và click link xác thực
7. Hệ thống kích hoạt tài khoản
8. Người dùng có thể đăng nhập

**Alternative Flow:**
- 3a. Email đã tồn tại → Hiển thị thông báo lỗi
- 6a. Link hết hạn → Yêu cầu gửi lại email xác thực

### UC-002: Đăng nhập hệ thống
**Mô tả:** Người dùng đăng nhập vào hệ thống
**Actor:** Người dùng đã đăng ký
**Preconditions:** Tài khoản đã được kích hoạt

**Main Flow:**
1. Người dùng nhập email và mật khẩu
2. Hệ thống xác thực thông tin
3. Hệ thống tạo JWT token
4. Người dùng được chuyển đến dashboard

**Alternative Flow:**
- 2a. Thông tin không đúng → Hiển thị thông báo lỗi
- 2b. Tài khoản bị khóa → Hiển thị thông báo và hướng dẫn

### UC-003: Quên mật khẩu
**Mô tả:** Người dùng reset mật khẩu khi quên
**Actor:** Người dùng
**Preconditions:** Có tài khoản trong hệ thống

**Main Flow:**
1. Người dùng click "Quên mật khẩu"
2. Nhập email đã đăng ký
3. Hệ thống gửi email reset mật khẩu
4. Người dùng click link trong email
5. Nhập mật khẩu mới
6. Hệ thống cập nhật mật khẩu
7. Người dùng có thể đăng nhập với mật khẩu mới

## 2. QUẢN LÝ HỒ SƠ NGƯỜI DÙNG

### UC-004: Cập nhật thông tin cá nhân
**Mô tả:** Người dùng cập nhật thông tin cá nhân
**Actor:** Người dùng đã đăng nhập
**Preconditions:** Đã đăng nhập hệ thống

**Main Flow:**
1. Người dùng truy cập trang profile
2. Chỉnh sửa thông tin: Họ tên, Số điện thoại, Địa chỉ
3. Hệ thống validate thông tin
4. Lưu thông tin mới
5. Hiển thị thông báo thành công

### UC-005: Tạo hồ sơ lái xe
**Mô tả:** Người dùng tạo hồ sơ lái xe để sử dụng dịch vụ
**Actor:** Người dùng đã đăng nhập
**Preconditions:** Đã đăng nhập hệ thống

**Main Flow:**
1. Người dùng truy cập "Tạo hồ sơ lái xe"
2. Nhập thông tin: Số bằng lái, Ngày cấp, Nơi cấp
3. Upload ảnh bằng lái
4. Hệ thống lưu hồ sơ với trạng thái "pending"
5. Admin xét duyệt hồ sơ
6. Hồ sơ được phê duyệt → Trạng thái "approved"

## 3. QUẢN LÝ TRẠM SẠC

### UC-006: Xem danh sách trạm sạc
**Mô tả:** Người dùng xem danh sách các trạm sạc có sẵn
**Actor:** Người dùng
**Preconditions:** Không cần đăng nhập

**Main Flow:**
1. Người dùng truy cập trang trạm sạc
2. Hệ thống hiển thị danh sách trạm sạc
3. Người dùng có thể lọc theo: Vị trí, Loại sạc, Trạng thái
4. Người dùng xem chi tiết từng trạm

### UC-007: Tìm kiếm trạm sạc gần nhất
**Mô tả:** Người dùng tìm trạm sạc gần vị trí hiện tại
**Actor:** Người dùng
**Preconditions:** Có quyền truy cập vị trí

**Main Flow:**
1. Người dùng cho phép truy cập vị trí
2. Hệ thống lấy tọa độ hiện tại
3. Tìm các trạm sạc trong bán kính 10km
4. Sắp xếp theo khoảng cách
5. Hiển thị danh sách trạm sạc gần nhất

### UC-008: Xem chi tiết trạm sạc
**Mô tả:** Người dùng xem thông tin chi tiết của trạm sạc
**Actor:** Người dùng
**Preconditions:** Đã chọn trạm sạc

**Main Flow:**
1. Người dùng click vào trạm sạc
2. Hệ thống hiển thị thông tin:
   - Địa chỉ, Số điện thoại
   - Các điểm sạc có sẵn
   - Giá sạc, Giờ hoạt động
   - Đánh giá và bình luận
3. Người dùng có thể đặt chỗ hoặc điều hướng

## 4. ĐẶT CHỖ VÀ QUẢN LÝ RESERVATION

### UC-009: Đặt chỗ sạc xe
**Mô tả:** Người dùng đặt chỗ sạc xe tại trạm sạc
**Actor:** Người dùng đã có hồ sơ lái xe
**Preconditions:** Hồ sơ lái xe đã được phê duyệt

**Main Flow:**
1. Người dùng chọn trạm sạc và điểm sạc
2. Chọn thời gian bắt đầu và kết thúc
3. Xem giá ước tính
4. Xác nhận đặt chỗ
5. Hệ thống tạo reservation với mã đặt chỗ
6. Gửi thông báo xác nhận
7. Hệ thống tự động gửi nhắc nhở trước 30 phút

**Alternative Flow:**
- 2a. Thời gian không khả dụng → Hiển thị thời gian khác
- 4a. Không đủ số dư → Yêu cầu nạp tiền

### UC-010: Hủy đặt chỗ
**Mô tả:** Người dùng hủy đặt chỗ đã tạo
**Actor:** Người dùng đã đặt chỗ
**Preconditions:** Có reservation đang hoạt động

**Main Flow:**
1. Người dùng truy cập "Đặt chỗ của tôi"
2. Chọn reservation cần hủy
3. Click "Hủy đặt chỗ"
4. Xác nhận hủy
5. Hệ thống cập nhật trạng thái "cancelled"
6. Hoàn tiền (nếu có) theo chính sách

### UC-011: Xem lịch sử đặt chỗ
**Mô tả:** Người dùng xem lịch sử các đặt chỗ
**Actor:** Người dùng đã đăng nhập
**Preconditions:** Đã đăng nhập hệ thống

**Main Flow:**
1. Người dùng truy cập "Lịch sử đặt chỗ"
2. Hệ thống hiển thị danh sách đặt chỗ:
   - Đang chờ, Đang sử dụng, Đã hoàn thành, Đã hủy
3. Người dùng có thể lọc theo trạng thái
4. Xem chi tiết từng đặt chỗ

## 5. QUẢN LÝ THANH TOÁN

### UC-012: Nạp tiền vào ví
**Mô tả:** Người dùng nạp tiền vào ví điện tử
**Actor:** Người dùng đã đăng nhập
**Preconditions:** Đã đăng nhập hệ thống

**Main Flow:**
1. Người dùng truy cập "Ví của tôi"
2. Click "Nạp tiền"
3. Nhập số tiền cần nạp
4. Chọn phương thức thanh toán
5. Thực hiện thanh toán
6. Hệ thống cập nhật số dư ví
7. Gửi thông báo xác nhận

### UC-013: Thanh toán đặt chỗ
**Mô tả:** Người dùng thanh toán cho đặt chỗ
**Actor:** Người dùng đã đặt chỗ
**Preconditions:** Có reservation cần thanh toán

**Main Flow:**
1. Hệ thống tự động trừ tiền từ ví khi đặt chỗ
2. Tạo giao dịch thanh toán
3. Gửi hóa đơn điện tử
4. Cập nhật số dư ví

**Alternative Flow:**
- 1a. Không đủ số dư → Yêu cầu nạp tiền trước

### UC-014: Xem lịch sử giao dịch
**Mô tả:** Người dùng xem lịch sử giao dịch
**Actor:** Người dùng đã đăng nhập
**Preconditions:** Đã đăng nhập hệ thống

**Main Flow:**
1. Người dùng truy cập "Lịch sử giao dịch"
2. Hệ thống hiển thị danh sách giao dịch:
   - Nạp tiền, Thanh toán, Hoàn tiền
3. Người dùng có thể lọc theo loại giao dịch
4. Xem chi tiết từng giao dịch

## 6. QUẢN LÝ PHIÊN SẠC

### UC-015: Bắt đầu phiên sạc
**Mô tả:** Người dùng bắt đầu sạc xe tại điểm sạc
**Actor:** Người dùng có reservation
**Preconditions:** Đã đến trạm sạc và có reservation

**Main Flow:**
1. Người dùng đến trạm sạc
2. Quét mã QR hoặc nhập mã đặt chỗ
3. Hệ thống xác thực reservation
4. Kết nối cáp sạc với xe
5. Bắt đầu phiên sạc
6. Hệ thống theo dõi tiến trình sạc

### UC-016: Kết thúc phiên sạc
**Mô tả:** Người dùng kết thúc phiên sạc
**Actor:** Người dùng đang sạc
**Preconditions:** Đang trong phiên sạc

**Main Flow:**
1. Người dùng ngắt kết nối cáp sạc
2. Hệ thống kết thúc phiên sạc
3. Tính toán chi phí dựa trên thời gian và công suất
4. Trừ tiền từ ví
5. Gửi hóa đơn chi tiết
6. Cập nhật trạng thái reservation

### UC-017: Theo dõi tiến trình sạc
**Mô tả:** Người dùng theo dõi tiến trình sạc
**Actor:** Người dùng đang sạc
**Preconditions:** Đang trong phiên sạc

**Main Flow:**
1. Người dùng mở app trong khi sạc
2. Hệ thống hiển thị thông tin:
   - Thời gian sạc, Công suất hiện tại
   - Phần trăm pin, Thời gian còn lại
   - Chi phí hiện tại
3. Người dùng có thể kết thúc sớm

## 7. QUẢN LÝ THÔNG BÁO

### UC-018: Nhận thông báo đặt chỗ
**Mô tả:** Người dùng nhận thông báo về đặt chỗ
**Actor:** Người dùng đã đặt chỗ
**Preconditions:** Có reservation đang hoạt động

**Main Flow:**
1. Hệ thống gửi thông báo nhắc nhở trước 30 phút
2. Người dùng nhận thông báo push/email
3. Người dùng có thể xem chi tiết đặt chỗ
4. Đánh dấu đã đọc

### UC-019: Quản lý cài đặt thông báo
**Mô tả:** Người dùng cài đặt loại thông báo muốn nhận
**Actor:** Người dùng đã đăng nhập
**Preconditions:** Đã đăng nhập hệ thống

**Main Flow:**
1. Người dùng truy cập "Cài đặt thông báo"
2. Chọn loại thông báo muốn nhận:
   - Nhắc nhở đặt chỗ
   - Thông báo thanh toán
   - Khuyến mãi
3. Lưu cài đặt

## 8. BÁO CÁO VÀ PHẢN HỒI

### UC-020: Báo cáo sự cố
**Mô tả:** Người dùng báo cáo sự cố tại trạm sạc
**Actor:** Người dùng
**Preconditions:** Đang sử dụng dịch vụ

**Main Flow:**
1. Người dùng click "Báo cáo sự cố"
2. Chọn loại sự cố: Hỏng thiết bị, Mất điện, Khác
3. Mô tả chi tiết sự cố
4. Upload ảnh (nếu có)
5. Gửi báo cáo
6. Hệ thống gửi thông báo xác nhận
7. Admin xử lý báo cáo

### UC-021: Đánh giá trạm sạc
**Mô tả:** Người dùng đánh giá trạm sạc sau khi sử dụng
**Actor:** Người dùng đã sử dụng dịch vụ
**Preconditions:** Đã hoàn thành phiên sạc

**Main Flow:**
1. Hệ thống gửi yêu cầu đánh giá
2. Người dùng chọn điểm đánh giá (1-5 sao)
3. Viết nhận xét (tùy chọn)
4. Gửi đánh giá
5. Hệ thống cập nhật điểm đánh giá trạm sạc

## 9. QUẢN TRỊ HỆ THỐNG (ADMIN)

### UC-022: Quản lý người dùng
**Mô tả:** Admin quản lý tài khoản người dùng
**Actor:** Admin
**Preconditions:** Đã đăng nhập với quyền admin

**Main Flow:**
1. Admin truy cập "Quản lý người dùng"
2. Xem danh sách người dùng
3. Có thể: Khóa/mở khóa tài khoản, Xem chi tiết, Xóa tài khoản
4. Xét duyệt hồ sơ lái xe

### UC-023: Quản lý trạm sạc
**Mô tả:** Admin quản lý trạm sạc và điểm sạc
**Actor:** Admin
**Preconditions:** Đã đăng nhập với quyền admin

**Main Flow:**
1. Admin truy cập "Quản lý trạm sạc"
2. Thêm/sửa/xóa trạm sạc
3. Quản lý điểm sạc trong trạm
4. Cập nhật thông tin: Giá, Giờ hoạt động, Trạng thái
5. Xem báo cáo sử dụng

### UC-024: Quản lý báo cáo sự cố
**Mô tả:** Admin xử lý báo cáo sự cố từ người dùng
**Actor:** Admin
**Preconditions:** Đã đăng nhập với quyền admin

**Main Flow:**
1. Admin truy cập "Báo cáo sự cố"
2. Xem danh sách báo cáo chưa xử lý
3. Xem chi tiết báo cáo
4. Cập nhật trạng thái: Đang xử lý, Đã xử lý
5. Gửi phản hồi cho người dùng

### UC-025: Xem báo cáo thống kê
**Mô tả:** Admin xem báo cáo thống kê hệ thống
**Actor:** Admin
**Preconditions:** Đã đăng nhập với quyền admin

**Main Flow:**
1. Admin truy cập "Báo cáo thống kê"
2. Chọn loại báo cáo:
   - Doanh thu theo thời gian
   - Sử dụng trạm sạc
   - Người dùng mới
   - Sự cố và phản hồi
3. Xuất báo cáo PDF/Excel

## 10. TÍNH NĂNG NÂNG CAO

### UC-026: Đặt chỗ định kỳ
**Mô tả:** Người dùng đặt chỗ sạc định kỳ hàng tuần
**Actor:** Người dùng thường xuyên
**Preconditions:** Đã có hồ sơ lái xe

**Main Flow:**
1. Người dùng chọn "Đặt chỗ định kỳ"
2. Chọn trạm sạc và thời gian cố định
3. Chọn ngày trong tuần
4. Xác nhận đặt chỗ định kỳ
5. Hệ thống tự động tạo reservation hàng tuần

### UC-027: Tìm kiếm trạm sạc theo tuyến đường
**Mô tả:** Người dùng tìm trạm sạc trên tuyến đường di chuyển
**Actor:** Người dùng
**Preconditions:** Có điểm xuất phát và đích đến

**Main Flow:**
1. Người dùng nhập điểm xuất phát và đích đến
2. Hệ thống tính toán tuyến đường
3. Hiển thị các trạm sạc trên tuyến đường
4. Gợi ý trạm sạc phù hợp với thời gian di chuyển
5. Người dùng có thể đặt chỗ trực tiếp

### UC-028: Chia sẻ trạm sạc
**Mô tả:** Người dùng chia sẻ trạm sạc với bạn bè
**Actor:** Người dùng
**Preconditions:** Đã đăng nhập hệ thống

**Main Flow:**
1. Người dùng chọn trạm sạc
2. Click "Chia sẻ"
3. Chọn phương thức chia sẻ: Link, QR code, Social media
4. Gửi thông tin trạm sạc
5. Người nhận có thể xem và đặt chỗ

---

## TỔNG KẾT

Hệ thống EV Charging Station Management bao gồm **28 use cases chính** được chia thành **10 nhóm chức năng**:

1. **Đăng ký và xác thực** (3 UC)
2. **Quản lý hồ sơ người dùng** (2 UC)  
3. **Quản lý trạm sạc** (3 UC)
4. **Đặt chỗ và quản lý reservation** (3 UC)
5. **Quản lý thanh toán** (3 UC)
6. **Quản lý phiên sạc** (3 UC)
7. **Quản lý thông báo** (2 UC)
8. **Báo cáo và phản hồi** (2 UC)
9. **Quản trị hệ thống** (4 UC)
10. **Tính năng nâng cao** (3 UC)

Mỗi use case đều có **Main Flow** rõ ràng và **Alternative Flow** để xử lý các trường hợp ngoại lệ, đảm bảo hệ thống hoạt động ổn định và user-friendly.
