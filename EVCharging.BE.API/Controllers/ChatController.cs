using EVCharging.BE.Common.DTOs.Chat;
using EVCharging.BE.Services.Services.Chat;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using FileIO = System.IO.File;

namespace EVCharging.BE.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ChatController : ControllerBase
    {
        private readonly IChatService _chatService;
        private readonly IChatDataService _chatDataService;

        public ChatController(IChatService chatService, IChatDataService chatDataService)
        {
            _chatService = chatService;
            _chatDataService = chatDataService;
        }

        /// <summary>
        /// Lấy danh sách câu hỏi mẫu
        /// </summary>
        [HttpGet("sample-questions")]
        [AllowAnonymous]
        public async Task<IActionResult> GetSampleQuestions()
        {
            try
            {
                var result = await _chatService.GetSampleQuestionsAsync();
                return Ok(new
                {
                    message = "Lấy danh sách câu hỏi mẫu thành công",
                    data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi lấy danh sách câu hỏi mẫu", error = ex.Message });
            }
        }

        /// <summary>
        /// Gửi câu hỏi và nhận câu trả lời
        /// </summary>
        [HttpPost("ask")]
        [AllowAnonymous]
        public async Task<IActionResult> AskQuestion([FromBody] ChatRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Question))
                {
                    return BadRequest(new { message = "Câu hỏi không được để trống" });
                }

                var result = await _chatService.GetAnswerAsync(request.Question);
                return Ok(new
                {
                    message = "Nhận câu trả lời thành công",
                    data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi xử lý câu hỏi", error = ex.Message });
            }
        }

        /// <summary>
        /// [Admin] Lấy tất cả Q&A data (để training)
        /// </summary>
        [HttpGet("qa-data")]
        [Authorize(Policy = "AdminPolicy")]
        public async Task<IActionResult> GetAllQAData()
        {
            try
            {
                var data = await _chatDataService.LoadQADataAsync();
                return Ok(new
                {
                    message = "Lấy Q&A data thành công",
                    data = data
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi lấy Q&A data", error = ex.Message });
            }
        }

        /// <summary>
        /// [Admin] Thêm hoặc cập nhật Q&A item (training)
        /// </summary>
        [HttpPost("qa-data")]
        [Authorize(Policy = "AdminPolicy")]
        public async Task<IActionResult> AddOrUpdateQA([FromBody] ChatQAItem item)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(item.Question) || string.IsNullOrWhiteSpace(item.Answer))
                {
                    return BadRequest(new { message = "Câu hỏi và câu trả lời không được để trống" });
                }

                await _chatDataService.AddOrUpdateQAAsync(item);
                return Ok(new
                {
                    message = "Thêm/cập nhật Q&A thành công",
                    data = item
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi thêm/cập nhật Q&A", error = ex.Message });
            }
        }

        /// <summary>
        /// [Admin] Xóa Q&A item
        /// </summary>
        [HttpDelete("qa-data/{id}")]
        [Authorize(Policy = "AdminPolicy")]
        public async Task<IActionResult> DeleteQA(string id)
        {
            try
            {
                var result = await _chatDataService.DeleteQAAsync(id);
                if (result)
                {
                    return Ok(new { message = "Xóa Q&A thành công" });
                }
                return NotFound(new { message = "Không tìm thấy Q&A với ID này" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi xóa Q&A", error = ex.Message });
            }
        }

        /// <summary>
        /// [Admin] Lấy Q&A theo ID
        /// </summary>
        [HttpGet("qa-data/{id}")]
        [Authorize(Policy = "AdminPolicy")]
        public async Task<IActionResult> GetQAById(string id)
        {
            try
            {
                var item = await _chatDataService.GetQAByIdAsync(id);
                if (item != null)
                {
                    return Ok(new
                    {
                        message = "Lấy Q&A thành công",
                        data = item
                    });
                }
                return NotFound(new { message = "Không tìm thấy Q&A với ID này" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi lấy Q&A", error = ex.Message });
            }
        }

        /// <summary>
        /// [Admin] Lấy thông tin đường dẫn file và download file JSON
        /// </summary>
        [HttpGet("qa-data/file-info")]
        [Authorize(Policy = "AdminPolicy")]
        public async Task<IActionResult> GetFileInfo()
        {
            try
            {
                var data = await _chatDataService.LoadQADataAsync();
                
                // Lấy đường dẫn file thực tế
                var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "chat-qa-data.json");
                var fileInfo = new FileInfo(filePath);
                
                return Ok(new
                {
                    message = "Thông tin file Q&A data",
                    filePath = filePath,
                    exists = FileIO.Exists(filePath),
                    fileSize = fileInfo.Exists ? fileInfo.Length : 0,
                    lastModified = fileInfo.Exists ? fileInfo.LastWriteTime : (DateTime?)null,
                    dataVersion = data.Version,
                    dataLastUpdated = data.LastUpdated,
                    totalQuestions = data.Questions.Count,
                    activeQuestions = data.Questions.Count(q => q.IsActive)
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi lấy thông tin file", error = ex.Message });
            }
        }

        /// <summary>
        /// [Admin] Download file JSON trực tiếp
        /// </summary>
        [HttpGet("qa-data/download")]
        [Authorize(Policy = "AdminPolicy")]
        public async Task<IActionResult> DownloadFile()
        {
            try
            {
                var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "chat-qa-data.json");
                
                if (!FileIO.Exists(filePath))
                {
                    return NotFound(new { message = "File không tồn tại" });
                }

                var fileBytes = await FileIO.ReadAllBytesAsync(filePath);
                var fileName = $"chat-qa-data-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json";
                
                return File(fileBytes, "application/json", fileName);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi download file", error = ex.Message });
            }
        }

        /// <summary>
        /// [Admin] Upload file JSON để import dữ liệu
        /// </summary>
        [HttpPost("qa-data/upload")]
        [Authorize(Policy = "AdminPolicy")]
        public async Task<IActionResult> UploadFile(IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return BadRequest(new { message = "Vui lòng chọn file để upload" });
                }

                if (!file.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(new { message = "File phải có định dạng JSON" });
                }

                using var stream = new MemoryStream();
                await file.CopyToAsync(stream);
                stream.Position = 0;

                var jsonContent = await new StreamReader(stream).ReadToEndAsync();
                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                var data = JsonSerializer.Deserialize<ChatQAData>(jsonContent, jsonOptions);

                if (data == null)
                {
                    return BadRequest(new { message = "File JSON không hợp lệ" });
                }

                await _chatDataService.SaveQADataAsync(data);
                
                return Ok(new
                {
                    message = "Upload và import dữ liệu thành công",
                    importedQuestions = data.Questions.Count
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi upload file", error = ex.Message });
            }
        }

        /// <summary>
        /// [Admin] Reset về dữ liệu mặc định (10 câu hỏi ban đầu)
        /// </summary>
        [HttpPost("qa-data/reset")]
        [Authorize(Policy = "AdminPolicy")]
        public async Task<IActionResult> ResetToDefault()
        {
            try
            {
                // Xóa file hiện tại và tạo lại với dữ liệu mặc định
                var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "chat-qa-data.json");
                
                if (FileIO.Exists(filePath))
                {
                    // Backup file cũ
                    var backupPath = filePath + $".backup.{DateTime.UtcNow:yyyyMMdd-HHmmss}";
                    FileIO.Copy(filePath, backupPath);
                }

                // Tạo lại service để trigger InitializeDefaultData
                var data = await _chatDataService.LoadQADataAsync();
                
                // Nếu file đã có nhưng muốn reset, ta cần xóa và tạo lại
                // Hoặc đơn giản là tạo lại dữ liệu mặc định
                var defaultData = new ChatQAData
                {
                    Version = "1.0",
                    LastUpdated = DateTime.UtcNow,
                    Questions = new List<ChatQAItem>
                    {
                        new ChatQAItem
                        {
                            Id = Guid.NewGuid().ToString(),
                            Question = "Làm thế nào để đăng ký tài khoản?",
                            Answer = "Bạn có thể đăng ký tài khoản bằng cách:\n1. Truy cập trang đăng ký\n2. Điền thông tin email và mật khẩu\n3. Xác nhận email qua OTP được gửi đến hộp thư\n4. Hoàn tất đăng ký và đăng nhập",
                            Keywords = new List<string> { "đăng ký", "tài khoản", "register", "account", "signup" },
                            Category = "account",
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        },
                        new ChatQAItem
                        {
                            Id = Guid.NewGuid().ToString(),
                            Question = "Làm sao để tìm trạm sạc gần nhất?",
                            Answer = "Để tìm trạm sạc gần nhất:\n1. Sử dụng tính năng tìm kiếm trên bản đồ\n2. Nhập địa chỉ hoặc cho phép truy cập vị trí của bạn\n3. Hệ thống sẽ hiển thị danh sách các trạm sạc gần bạn\n4. Bạn có thể xem thông tin chi tiết và đặt chỗ trước",
                            Keywords = new List<string> { "tìm", "trạm sạc", "gần nhất", "location", "search", "địa chỉ", "vị trí" },
                            Category = "station",
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        },
                        new ChatQAItem
                        {
                            Id = Guid.NewGuid().ToString(),
                            Question = "Các phương thức thanh toán nào được hỗ trợ?",
                            Answer = "Hệ thống hỗ trợ các phương thức thanh toán sau:\n1. Ví điện tử (Wallet) - nạp tiền và thanh toán trực tiếp\n2. VNPay - thanh toán qua cổng VNPay\n3. MoMo - thanh toán qua ví MoMo\n4. Thanh toán khi sử dụng dịch vụ",
                            Keywords = new List<string> { "thanh toán", "payment", "phương thức", "ví", "wallet", "vnpay", "momo", "tiền" },
                            Category = "payment",
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        },
                        new ChatQAItem
                        {
                            Id = Guid.NewGuid().ToString(),
                            Question = "Làm thế nào để đặt chỗ trạm sạc?",
                            Answer = "Để đặt chỗ trạm sạc:\n1. Chọn trạm sạc bạn muốn sử dụng\n2. Chọn thời gian bắt đầu và kết thúc\n3. Xác nhận thông tin đặt chỗ\n4. Thanh toán (nếu cần)\n5. Nhận mã QR để check-in tại trạm",
                            Keywords = new List<string> { "đặt chỗ", "reservation", "booking", "qr", "qr code", "mã qr", "check-in" },
                            Category = "reservation",
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        },
                        new ChatQAItem
                        {
                            Id = Guid.NewGuid().ToString(),
                            Question = "Giá sạc được tính như thế nào?",
                            Answer = "Giá sạc được tính dựa trên:\n1. Loại gói đăng ký của bạn (nếu có)\n2. Thời gian sạc (theo giờ hoặc phút)\n3. Loại trạm sạc (nhanh/chậm)\n4. Thời điểm sử dụng (giờ cao điểm/giờ thấp điểm)\nBạn có thể xem bảng giá chi tiết trong phần Pricing Plans",
                            Keywords = new List<string> { "giá", "phí", "chi phí", "price", "cost", "fee", "tính", "bảng giá", "pricing" },
                            Category = "pricing",
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        },
                        new ChatQAItem
                        {
                            Id = Guid.NewGuid().ToString(),
                            Question = "Làm sao để hủy đặt chỗ?",
                            Answer = "Để hủy đặt chỗ:\n1. Vào phần 'Đặt chỗ của tôi'\n2. Chọn đặt chỗ bạn muốn hủy\n3. Nhấn nút 'Hủy đặt chỗ'\n4. Xác nhận hủy\nLưu ý: Hủy trước thời gian đặt chỗ sẽ được hoàn tiền (nếu đã thanh toán)",
                            Keywords = new List<string> { "hủy", "cancel", "đặt chỗ", "reservation", "hoàn tiền", "refund" },
                            Category = "reservation",
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        },
                        new ChatQAItem
                        {
                            Id = Guid.NewGuid().ToString(),
                            Question = "Tôi quên mật khẩu, làm sao để lấy lại?",
                            Answer = "Để lấy lại mật khẩu:\n1. Vào trang 'Quên mật khẩu'\n2. Nhập email đã đăng ký\n3. Kiểm tra email và nhận link đặt lại mật khẩu\n4. Tạo mật khẩu mới\n5. Đăng nhập lại với mật khẩu mới",
                            Keywords = new List<string> { "quên mật khẩu", "forgot password", "reset password", "lấy lại", "mật khẩu", "password" },
                            Category = "account",
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        },
                        new ChatQAItem
                        {
                            Id = Guid.NewGuid().ToString(),
                            Question = "Làm thế nào để nạp tiền vào ví?",
                            Answer = "Để nạp tiền vào ví:\n1. Vào phần 'Ví của tôi'\n2. Chọn 'Nạp tiền'\n3. Nhập số tiền muốn nạp\n4. Chọn phương thức thanh toán (VNPay, MoMo, hoặc MockPay)\n5. Hoàn tất thanh toán\n6. Tiền sẽ được cập nhật vào ví sau khi thanh toán thành công",
                            Keywords = new List<string> { "nạp tiền", "topup", "deposit", "ví", "wallet", "tiền", "money" },
                            Category = "payment",
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        },
                        new ChatQAItem
                        {
                            Id = Guid.NewGuid().ToString(),
                            Question = "Tôi có thể xem lịch sử giao dịch không?",
                            Answer = "Có, bạn có thể xem lịch sử giao dịch:\n1. Vào phần 'Lịch sử thanh toán'\n2. Xem tất cả các giao dịch đã thực hiện\n3. Lọc theo thời gian, loại giao dịch\n4. Tải hóa đơn (invoice) nếu cần",
                            Keywords = new List<string> { "lịch sử", "history", "giao dịch", "transaction", "thanh toán", "payment", "hóa đơn", "invoice" },
                            Category = "payment",
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        },
                        new ChatQAItem
                        {
                            Id = Guid.NewGuid().ToString(),
                            Question = "Làm sao để liên hệ hỗ trợ?",
                            Answer = "Bạn có thể liên hệ hỗ trợ qua:\n1. Chatbox này - đặt câu hỏi bất kỳ\n2. Email hỗ trợ: support@evcharging.com\n3. Hotline: 1900-xxxx\n4. Tạo báo cáo sự cố trong phần 'Báo cáo sự cố'",
                            Keywords = new List<string> { "liên hệ", "hỗ trợ", "support", "contact", "help", "báo cáo", "report", "sự cố", "issue" },
                            Category = "support",
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        }
                    }
                };

                await _chatDataService.SaveQADataAsync(defaultData);
                
                return Ok(new
                {
                    message = "Đã reset về dữ liệu mặc định thành công",
                    totalQuestions = defaultData.Questions.Count
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi reset dữ liệu", error = ex.Message });
            }
        }
    }
}

