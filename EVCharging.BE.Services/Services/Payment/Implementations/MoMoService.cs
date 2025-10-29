using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EVCharging.BE.Services.Services.Payment;
using Microsoft.Extensions.Configuration;

namespace EVCharging.BE.Services.Services.Payment.Implementations
{
    public class MoMoService : IMoMoService
    {
        private readonly IConfiguration _cfg;
        private readonly HttpClient _http;

        // Dùng IHttpClientFactory: nhớ AddHttpClient("momo", ...) ở Program.cs
        public MoMoService(IConfiguration cfg, IHttpClientFactory httpClientFactory)
        {
            _cfg = cfg;
            _http = httpClientFactory.CreateClient("momo");
        }

        // ✅ MỚI: trả payUrl + deeplink
        public async Task<MoMoCreateResult> CreatePayAsync(string orderId, decimal amount, string orderInfo)
        {
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));

            var s = _cfg.GetSection("MoMo");
            var partnerCode = s["PartnerCode"]!;
            var accessKey = s["AccessKey"]!;
            var secretKey = s["SecretKey"]!;
            var endpoint = s["Endpoint"]!;
            var returnUrl = s["ReturnUrl"]!;
            var ipnUrl = s["IpnUrl"]!;

            var requestId = Guid.NewGuid().ToString("N");
            var amt = ((long)amount).ToString(); // VND nguyên, không thập phân
            const string requestType = "captureWallet";
            const string extraData = "";

            var raw = $"accessKey={accessKey}&amount={amt}&extraData={extraData}&ipnUrl={ipnUrl}" +
                      $"&orderId={orderId}&orderInfo={orderInfo}&partnerCode={partnerCode}" +
                      $"&redirectUrl={returnUrl}&requestId={requestId}&requestType={requestType}";

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
            var signature = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();

            var body = new
            {
                partnerCode,
                accessKey,
                requestId,
                amount = amt,
                orderId,
                orderInfo,
                redirectUrl = returnUrl,
                ipnUrl,
                extraData,
                requestType,
                signature,
                lang = "vi"
            };

            using var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            var res = await _http.PostAsync($"{endpoint}/v2/gateway/api/create", content);
            var text = await res.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;

            if (!res.IsSuccessStatusCode)
                throw new InvalidOperationException($"MoMo HTTP {(int)res.StatusCode}: {text}");

            var resultCode = root.TryGetProperty("resultCode", out var rcEl) ? rcEl.GetInt32() : -1;
            if (resultCode != 0)
            {
                var msg = root.TryGetProperty("message", out var msgEl) ? msgEl.GetString() : "Unknown";
                throw new InvalidOperationException($"MoMo create failed (resultCode={resultCode}): {msg}");
            }

            var payUrl = root.GetProperty("payUrl").GetString()!;
            var deeplink = root.TryGetProperty("deeplink", out var dl) ? dl.GetString() : null;

            return new MoMoCreateResult(payUrl, deeplink);
        }

        // ♻️ Giữ method cũ cho tương thích — gọi lại method mới
        public async Task<string> CreatePayUrlAsync(string orderId, decimal amount, string orderInfo)
        {
            var r = await CreatePayAsync(orderId, amount, orderInfo);
            return r.PayUrl;
        }

        public bool VerifyIpnSignature(Dictionary<string, string> form)
        {
            var s = _cfg.GetSection("MoMo");
            var secretKey = s["SecretKey"]!;
            var accessKey = s["AccessKey"]!;

            string Get(string k) => form.TryGetValue(k, out var v) ? v : "";

            // Chuỗi ký theo tài liệu IPN v2
            var raw = $"accessKey={accessKey}&amount={Get("amount")}&extraData={Get("extraData")}" +
                      $"&message={Get("message")}&orderId={Get("orderId")}&orderInfo={Get("orderInfo")}" +
                      $"&orderType={Get("orderType")}&partnerCode={Get("partnerCode")}&payType={Get("payType")}" +
                      $"&requestId={Get("requestId")}&responseTime={Get("responseTime")}" +
                      $"&resultCode={Get("resultCode")}&transId={Get("transId")}";

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
            var calc = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
            return string.Equals(calc, Get("signature"), StringComparison.OrdinalIgnoreCase);
        }
    }
}
