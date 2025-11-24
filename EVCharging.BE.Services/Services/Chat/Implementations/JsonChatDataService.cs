using EVCharging.BE.Common.DTOs.Chat;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace EVCharging.BE.Services.Services.Chat.Implementations
{
    public class JsonChatDataService : IChatDataService
    {
        private readonly string _dataFilePath;
        private readonly ILogger<JsonChatDataService> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public JsonChatDataService(ILogger<JsonChatDataService> logger, IConfiguration configuration)
        {
            _logger = logger;
            
            // Lấy đường dẫn từ config hoặc dùng thư mục hiện tại
            var dataPath = configuration["ChatData:FilePath"] 
                ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
            
            var dataDirectory = Path.GetDirectoryName(dataPath);
            if (string.IsNullOrEmpty(dataDirectory))
            {
                dataDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
            }

            if (!Directory.Exists(dataDirectory))
            {
                Directory.CreateDirectory(dataDirectory);
            }

            _dataFilePath = Path.Combine(dataDirectory, "chat-qa-data.json");

            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            // Tạo file mẫu nếu chưa tồn tại
            if (!File.Exists(_dataFilePath))
            {
                InitializeDefaultData();
            }
        }

        private void InitializeDefaultData()
        {
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

            SaveQADataAsync(defaultData).Wait();
            _logger.LogInformation("Đã tạo file Q&A data mặc định với {Count} câu hỏi tại: {Path}", defaultData.Questions.Count, _dataFilePath);
        }

        public async Task<ChatQAData> LoadQADataAsync()
        {
            try
            {
                if (!File.Exists(_dataFilePath))
                {
                    _logger.LogWarning("File Q&A data không tồn tại, tạo file mặc định");
                    InitializeDefaultData();
                }

                var jsonContent = await File.ReadAllTextAsync(_dataFilePath);
                var data = JsonSerializer.Deserialize<ChatQAData>(jsonContent, _jsonOptions);

                if (data == null)
                {
                    _logger.LogWarning("Không thể đọc Q&A data, tạo file mặc định");
                    InitializeDefaultData();
                    jsonContent = await File.ReadAllTextAsync(_dataFilePath);
                    data = JsonSerializer.Deserialize<ChatQAData>(jsonContent, _jsonOptions) ?? new ChatQAData();
                }

                return data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi đọc Q&A data từ file");
                return new ChatQAData();
            }
        }

        public async Task SaveQADataAsync(ChatQAData data)
        {
            try
            {
                data.LastUpdated = DateTime.UtcNow;
                var jsonContent = JsonSerializer.Serialize(data, _jsonOptions);
                
                // Write to temp file first, then replace (atomic operation)
                var tempFilePath = _dataFilePath + ".tmp";
                await File.WriteAllTextAsync(tempFilePath, jsonContent);
                File.Move(tempFilePath, _dataFilePath, overwrite: true);
                
                _logger.LogInformation("Đã lưu Q&A data thành công");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lưu Q&A data vào file");
                throw;
            }
        }

        public async Task AddOrUpdateQAAsync(ChatQAItem item)
        {
            var data = await LoadQADataAsync();
            
            var existingItem = data.Questions.FirstOrDefault(q => q.Id == item.Id);
            if (existingItem != null)
            {
                // Update existing
                existingItem.Question = item.Question;
                existingItem.Answer = item.Answer;
                existingItem.Keywords = item.Keywords;
                existingItem.Category = item.Category;
                existingItem.IsActive = item.IsActive;
                existingItem.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                // Add new
                if (string.IsNullOrEmpty(item.Id))
                {
                    item.Id = Guid.NewGuid().ToString();
                }
                item.CreatedAt = DateTime.UtcNow;
                item.UpdatedAt = DateTime.UtcNow;
                data.Questions.Add(item);
            }

            await SaveQADataAsync(data);
        }

        public async Task<bool> DeleteQAAsync(string id)
        {
            var data = await LoadQADataAsync();
            var item = data.Questions.FirstOrDefault(q => q.Id == id);
            
            if (item != null)
            {
                data.Questions.Remove(item);
                await SaveQADataAsync(data);
                return true;
            }

            return false;
        }

        public async Task<ChatQAItem?> GetQAByIdAsync(string id)
        {
            var data = await LoadQADataAsync();
            return data.Questions.FirstOrDefault(q => q.Id == id);
        }
    }
}

