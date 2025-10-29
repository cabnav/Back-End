using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace EVCharging.BE.Services.Services.Payment.Implementations
{
    public class VNPayService : IVNPayService
    {
        private readonly IConfiguration _cfg;
        public VNPayService(IConfiguration cfg) => _cfg = cfg;

        /// <summary>
        /// Tạo URL thanh toán VNPay (sandbox/prod tuỳ appsettings)
        /// </summary>
        public string BuildPayUrl(string code, decimal amount, string orderInfo, HttpRequest req)
        {
            var sec = _cfg.GetSection("VNPay");
            var tmn = sec["TmnCode"] ?? throw new InvalidOperationException("VNPay:TmnCode missing");
            var secret = sec["HashSecret"] ?? throw new InvalidOperationException("VNPay:HashSecret missing");
            var payUrl = sec["PaymentUrl"] ?? throw new InvalidOperationException("VNPay:PaymentUrl missing");
            var returnUrl = sec["ReturnUrl"] ?? throw new InvalidOperationException("VNPay:ReturnUrl missing");
            var ipnUrl = sec["IpnUrl"]; // ✅ Cho phép empty khi test local

            // TxnRef gọn, hợp lệ, độ dài nên 8-20 ký tự (không bắt buộc cứng nhưng khuyến nghị)
            var txnRef = SanitizeTxnRef(code);

            // Lấy IP thật nếu chạy sau reverse proxy
            var clientIp = GetClientIp(req);

            // ✅ Dùng giờ Việt Nam (SE Asia Standard Time = UTC+7)
            var vnTime = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow,
                TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time")
            );

            var fields = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["vnp_Version"] = "2.1.0",
                ["vnp_Command"] = "pay",
                ["vnp_TmnCode"] = tmn,
                ["vnp_Amount"] = ((long)(amount * 100)).ToString(CultureInfo.InvariantCulture), // VND * 100
                ["vnp_CurrCode"] = "VND",
                ["vnp_TxnRef"] = txnRef,
                ["vnp_OrderInfo"] = orderInfo ?? string.Empty,
                ["vnp_OrderType"] = "other",
                ["vnp_Locale"] = "vn",
                ["vnp_CreateDate"] = vnTime.ToString("yyyyMMddHHmmss"),
                // ✅ Tạm thời bỏ ExpireDate để test (có thể gây lỗi)
                // ["vnp_ExpireDate"] = vnTime.AddMinutes(15).ToString("yyyyMMddHHmmss"),
                ["vnp_IpAddr"] = clientIp,
                ["vnp_ReturnUrl"] = returnUrl

                // ✅ Bỏ vnp_BankCode để VNPay tự chọn ngân hàng
                // Một số tài khoản sandbox không hỗ trợ VNPAYQR
                // ["vnp_BankCode"] = "VNPAYQR"
            };

            // ✅ Chỉ thêm IpnUrl nếu có giá trị (tránh lỗi khi test local)
            if (!string.IsNullOrWhiteSpace(ipnUrl))
            {
                fields["vnp_IpnUrl"] = ipnUrl;
            }

            // ✅ Bước 1: Tạo chuỗi để hash (ENCODE value - giống VerifySignature)
            static string E(string v) => Uri.EscapeDataString(v ?? string.Empty);
            var hashData = string.Join("&", fields.Select(kv => $"{kv.Key}={E(kv.Value)}"));

            // ✅ Bước 2: Tính HMAC SHA512
            using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(secret));
            var sign = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(hashData)));

            // ✅ Bước 3: Build URL với value đã encode
            var queryParams = string.Join("&", fields.Select(kv => $"{kv.Key}={E(kv.Value)}"));
            var query = queryParams + "&vnp_SecureHashType=HMACSHA512&vnp_SecureHash=" + sign;

            return $"{payUrl}?{query}";
        }

        /// <summary>
        /// Verify chữ ký cho Return/IPN (query hoặc form đều đọc như nhau)
        /// </summary>
        public bool VerifySignature(IQueryCollection query, out Dictionary<string, string> data)
        {
            var secret = _cfg.GetValue<string>("VNPay:HashSecret")
                         ?? throw new InvalidOperationException("VNPay:HashSecret missing");

            data = new();
            foreach (var kv in query)
                if (kv.Key.StartsWith("vnp_", StringComparison.Ordinal))
                    data[kv.Key] = kv.Value.ToString();

            // Lấy hash gốc, loại nó và HashType khỏi data trước khi ký lại
            if (!data.TryGetValue("vnp_SecureHash", out var hash)) return false;
            data.Remove("vnp_SecureHash");
            data.Remove("vnp_SecureHashType");

            var sorted = new SortedDictionary<string, string>(data, StringComparer.Ordinal);
            static string E(string v) => Uri.EscapeDataString(v ?? string.Empty);
            var raw = string.Join("&", sorted.Select(kv => $"{kv.Key}={E(kv.Value)}"));

            using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(secret));
            var calc = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(raw)));

            // So sánh không phân biệt hoa/thường
            return string.Equals(calc, hash, StringComparison.OrdinalIgnoreCase);
        }

        private static string GetClientIp(HttpRequest req)
        {
            // Ưu tiên header proxy
            var ip = req.Headers["X-Forwarded-For"].FirstOrDefault()
                  ?? req.Headers["X-Real-IP"].FirstOrDefault()
                  ?? req.HttpContext.Connection.RemoteIpAddress?.ToString()
                  ?? "127.0.0.1";
            // Nếu có nhiều IP (proxy chain) thì lấy IP đầu
            var comma = ip.IndexOf(',');
            return comma > 0 ? ip[..comma].Trim() : ip.Trim();
        }

        private static string SanitizeTxnRef(string input)
        {
            // Giữ A-Z a-z 0-9 - _ ; cắt về 20 ký tự để gọn, nếu ngắn quá thì pad timestamp
            var sb = new StringBuilder();
            foreach (var ch in input)
            {
                if (char.IsLetterOrDigit(ch) || ch == '-' || ch == '_')
                    sb.Append(ch);
            }
            var s = sb.ToString();
            if (s.Length < 8) s += DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            if (s.Length > 20) s = s[..20];
            return s.ToUpperInvariant();
        }


    }
}
