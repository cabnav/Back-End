using EVCharging.BE.Common.DTOs.Payments;
using System.Text;
using QRCoder;
using Microsoft.AspNetCore.Mvc;
using EVCharging.BE.Services.Services.Payment;
using System.Security.Claims;
using EVCharging.BE.DAL;
using Microsoft.EntityFrameworkCore;
using PaymentEntity = EVCharging.BE.DAL.Entities.Payment;

[ApiController]
[Route("api/[controller]")]
public class WalletTransactionsController : ControllerBase
{
    private readonly IMockPayService _mock;
    private readonly IMomoService _momoService;
    private readonly EvchargingManagementContext _db;

    public WalletTransactionsController(IMockPayService mock, IMomoService momoService, EvchargingManagementContext db)
    {
        _mock = mock;
        _momoService = momoService;
        _db = db;
    }

    // 1) Tạo yêu cầu nạp → trả code + link + QR
    [HttpPost("topup/request")]
    public async Task<IActionResult> CreateTopUp([FromBody] WalletTopUpRequestDto req)
    {
        var (code, base64, expires) = await _mock.CreateTopUpAsync(req.UserId, req.Amount);
        var checkoutUrl = $"{Request.Scheme}://{Request.Host}/mockpay/checkout/{code}";
        var qrImageUrl = $"{Request.Scheme}://{Request.Host}/api/wallettransactions/topup/qr/{code}";

        return Ok(new
        {
            code,
            checkout_url = checkoutUrl,
            qr_image_url = qrImageUrl,
            qr_base64 = $"data:image/png;base64,{base64}",
            expires_at_utc = expires
        });
    }

    // 1b) Tạo yêu cầu nạp bằng MoMo (thực)
    [HttpPost("topup/momo")]
    public async Task<IActionResult> CreateTopUpMomo([FromBody] WalletTopUpRequestDto req)
    {
        if (req == null || req.Amount <= 0)
            return BadRequest(new { message = "Amount phải > 0" });

        // Lấy userId từ JWT token nếu có, ngược lại dùng req.UserId
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        int currentUserId;
        if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out var parsed))
        {
            currentUserId = parsed;
        }
        else
        {
            currentUserId = req.UserId;
        }

        // Tạo Payment record (pending)
        var tempInvoiceNumber = $"TOPUP_{currentUserId}_{DateTime.UtcNow:yyyyMMddHHmmss}";
        if (tempInvoiceNumber.Length > 50) tempInvoiceNumber = tempInvoiceNumber.Substring(0, 50);

        // Ensure uniqueness
        var original = tempInvoiceNumber;
        int suffix = 0;
        while (await _db.Payments.AnyAsync(p => p.InvoiceNumber == tempInvoiceNumber))
        {
            suffix++;
            var suf = suffix.ToString();
            tempInvoiceNumber = original.Length + suf.Length <= 50 ? original + suf : original.Substring(0, 50 - suf.Length) + suf;
        }

        var payment = new PaymentEntity
        {
            UserId = currentUserId,
            Amount = req.Amount,
            PaymentMethod = "momo",
            PaymentStatus = "pending",
            InvoiceNumber = tempInvoiceNumber,
            CreatedAt = DateTime.UtcNow
        };

        _db.Payments.Add(payment);
        await _db.SaveChangesAsync();

        // Tạo MoMo request
        var user = await _db.Users.FindAsync(currentUserId);
        var fullName = user?.Name ?? "Khách hàng";

        var momoRequest = new MomoCreatePaymentRequestDto
        {
            SessionId = 0, // không có session => xác định đây là top-up
            UserId = currentUserId,
            Amount = req.Amount,
            FullName = fullName,
            OrderInfo = $"Nạp tiền vào ví - UserId:{currentUserId}"
        };

        var momoResponse = await _momoService.CreatePaymentAsync(momoRequest);

        // Cập nhật InvoiceNumber với orderId từ MoMo (nếu có)
        var finalInvoice = momoResponse.OrderId ?? payment.InvoiceNumber;
        if (finalInvoice.Length > 50) finalInvoice = finalInvoice.Substring(0, 50);
        if (!await _db.Payments.AnyAsync(p => p.InvoiceNumber == finalInvoice && p.PaymentId != payment.PaymentId))
        {
            payment.InvoiceNumber = finalInvoice;
            await _db.SaveChangesAsync();
        }

        if (momoResponse.ErrorCode != 0)
        {
            _db.Payments.Remove(payment);
            await _db.SaveChangesAsync();

            return BadRequest(new
            {
                message = $"Lỗi khi tạo payment: {momoResponse.Message}",
                errorCode = momoResponse.ErrorCode
            });
        }

        var checkoutUrl = momoResponse.PayUrl;
        var qrImageUrl = $"{Request.Scheme}://{Request.Host}/api/wallettransactions/topup/qr/{momoResponse.OrderId}";

        return Ok(new
        {
            message = "Tạo payment URL thành công",
            payUrl = checkoutUrl,
            orderId = momoResponse.OrderId,
            qrCodeUrl = momoResponse.QrCodeUrl,
            deeplink = momoResponse.Deeplink,
            paymentId = payment.PaymentId,
            qr_image_url = qrImageUrl
        });
    }

    [HttpGet("topup/qr/{code}")]
    [Produces("image/png")]
    public IActionResult GetQrImage(string code)
    {
        var url = $"{Request.Scheme}://{Request.Host}/mockpay/checkout/{code}";
        using var gen = new QRCodeGenerator();
        using var data = gen.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
        return File(new PngByteQRCode(data).GetGraphic(8), "image/png");
    }

    [HttpPost("topup/mock-callback")]
    public async Task<IActionResult> MockCallback([FromForm] string code, [FromForm] bool success)
    {
        var ok = await _mock.ConfirmAsync(code, success);
        if (!ok) return NotFound(new { message = "Code not found" });

        var isSuccess = success;
        var statusText = isSuccess ? "THÀNH CÔNG" : "THẤT BẠI";
        var statusIcon = isSuccess ? "✅" : "❌";
        var statusColor = isSuccess ? "#667eea" : "#f5576c";
        var bgGradient = isSuccess 
            ? "linear-gradient(135deg, #667eea 0%, #764ba2 100%)" 
            : "linear-gradient(135deg, #f093fb 0%, #f5576c 100%)";
        
        var html = $@"<!doctype html>
<html lang='vi'>
<head>
  <meta charset='utf-8'>
  <meta name='viewport' content='width=device-width, initial-scale=1.0'>
  <title>Kết quả thanh toán - {statusText}</title>
  <style>
    * {{ margin: 0; padding: 0; box-sizing: border-box; }}
    body {{
      font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif;
      background: {bgGradient};
      min-height: 100vh;
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 20px;
    }}
    .container {{
      background: white;
      border-radius: 20px;
      box-shadow: 0 20px 60px rgba(0,0,0,0.3);
      max-width: 500px;
      width: 100%;
      padding: 50px 40px;
      text-align: center;
      animation: fadeInScale 0.6s ease-out;
    }}
    @keyframes fadeInScale {{
      from {{
        opacity: 0;
        transform: scale(0.9) translateY(-20px);
      }}
      to {{
        opacity: 1;
        transform: scale(1) translateY(0);
      }}
    }}
    .status-icon {{
      font-size: 80px;
      margin-bottom: 20px;
      animation: bounceIn 0.8s ease-out;
    }}
    @keyframes bounceIn {{
      0% {{ transform: scale(0); }}
      50% {{ transform: scale(1.2); }}
      100% {{ transform: scale(1); }}
    }}
    .status-title {{
      font-size: 32px;
      font-weight: 700;
      color: {statusColor};
      margin-bottom: 15px;
      text-transform: uppercase;
      letter-spacing: 1px;
    }}
    .status-message {{
      color: #666;
      font-size: 16px;
      margin-bottom: 30px;
      line-height: 1.6;
    }}
    .info-box {{
      background: #f8f9fa;
      border-radius: 12px;
      padding: 20px;
      margin-bottom: 30px;
      text-align: left;
    }}
    .info-label {{
      color: #999;
      font-size: 12px;
      text-transform: uppercase;
      letter-spacing: 0.5px;
      margin-bottom: 5px;
    }}
    .info-value {{
      color: #333;
      font-size: 18px;
      font-weight: 600;
      font-family: 'Courier New', monospace;
    }}
    .action-button {{
      display: inline-block;
      padding: 14px 30px;
      background: {statusColor};
      color: white;
      text-decoration: none;
      border-radius: 10px;
      font-weight: 600;
      transition: all 0.3s ease;
      box-shadow: 0 4px 15px rgba(0,0,0,0.2);
    }}
    .action-button:hover {{
      transform: translateY(-2px);
      box-shadow: 0 6px 20px rgba(0,0,0,0.3);
    }}
    .action-button:active {{
      transform: translateY(0);
    }}
    .footer {{
      margin-top: 30px;
      color: #999;
      font-size: 12px;
    }}
    @media (max-width: 480px) {{
      .container {{
        padding: 40px 25px;
      }}
      .status-icon {{
        font-size: 60px;
      }}
      .status-title {{
        font-size: 24px;
      }}
    }}
  </style>
</head>
<body>
  <div class='container'>
    <div class='status-icon'>{statusIcon}</div>
    <h1 class='status-title'>{statusText}</h1>
    <p class='status-message'>
      {(isSuccess 
        ? "Giao dịch của bạn đã được xử lý thành công. Số tiền đã được nạp vào ví." 
        : "Giao dịch thất bại. Vui lòng thử lại hoặc liên hệ hỗ trợ nếu cần.")}
    </p>
    <div class='info-box'>
      <div class='info-label'>Mã giao dịch</div>
      <div class='info-value'>{code}</div>
    </div>
    <a href='javascript:window.close()' class='action-button'>Đóng cửa sổ</a>
    <div class='footer'>
      <p>MockPay - Hệ thống thanh toán demo</p>
    </div>
  </div>
</body></html>";
        return Content(html, "text/html", Encoding.UTF8);
    }

    [HttpGet("topup/status/{code}")]
    public async Task<IActionResult> GetStatus(string code)
    {
        var st = await _mock.GetStatusAsync(code);
        return st is null ? NotFound() : Ok(new { code, status = st });
    }
}