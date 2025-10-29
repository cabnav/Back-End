using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using EVCharging.BE.Services.Services.Payment;
using EVCharging.BE.Common.DTOs.Payments;
using EVCharging.BE.DAL;
using EVCharging.BE.Services.Services.Payment.Implementations;
using PaymentEntity = EVCharging.BE.DAL.Entities.Payment;

[ApiController]
[Route("api/[controller]")]
public class WalletTransactionsController : ControllerBase
{
    private readonly IMockPayService _mock;
    private readonly IVNPayService _vnp;
    private readonly IMoMoService _momo;
    private readonly EvchargingManagementContext _db;
    private readonly IWalletService _wallet;

    public WalletTransactionsController(IMockPayService mock, IVNPayService vnp, IMoMoService momo,
                                        EvchargingManagementContext db, IWalletService wallet)
    {
        _mock = mock; _vnp = vnp; _momo = momo; _db = db; _wallet = wallet;
    }

    // ---------- MOCK (cũ) vẫn giữ để test nhanh nếu cần ----------
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

        var html = @"<!doctype html><html lang='vi'><head><meta charset='utf-8'>
<title>Kết quả</title></head><body style='font-family:sans-serif;padding:24px'>
<h3>Kết quả: " + (success ? "THÀNH CÔNG" : "THẤT BẠI") + @"</h3></body></html>";
        return Content(html, "text/html", Encoding.UTF8);
    }

    [HttpGet("topup/status/{code}")]
    public async Task<IActionResult> GetStatus(string code)
    {
        var st = await _mock.GetStatusAsync(code);
        return st is null ? NotFound() : Ok(new { code, status = st });
    }

    // ---------- VNPAY ----------
    [HttpPost("topup/vnpay/request")]
    public async Task<IActionResult> VnpayRequest([FromBody] WalletTopUpRequestDto req)
    {
        var code = $"TP{Guid.NewGuid():N}".ToUpperInvariant();

        _db.Payments.Add(new PaymentEntity
        {
            UserId = req.UserId,
            Amount = req.Amount,
            PaymentMethod = "vnpay",
            PaymentStatus = "pending",
            InvoiceNumber = code,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var payUrl = _vnp.BuildPayUrl(code, req.Amount, $"WALLET_TOPUP_{req.UserId}", Request);
        return Ok(new { code, redirect_url = payUrl }); // mở URL này sẽ "nhảy" sang VNPAY (có QR)
    }

    // Trang Return của VNPAY (cộng ví nếu thành công - để test khi không có IPN)
    [HttpGet("vnpay/return")]
    public async Task<IActionResult> VnpayReturn()
    {
        var ok = _vnp.VerifySignature(Request.Query, out var data);
        var status = ok && data.TryGetValue("vnp_ResponseCode", out var code) && code == "00"
                        ? "THÀNH CÔNG" : "THẤT BẠI";

        var txnRef = data.GetValueOrDefault("vnp_TxnRef", "");
        var responseCode = data.GetValueOrDefault("vnp_ResponseCode", "");

        // ✅ Cộng ví nếu thanh toán thành công (khi IPN không hoạt động)
        if (ok && responseCode == "00" && !string.IsNullOrEmpty(txnRef))
        {
            var pay = await _db.Payments.FirstOrDefaultAsync(p =>
                p.InvoiceNumber == txnRef && p.PaymentMethod == "vnpay");

            if (pay != null && pay.PaymentStatus != "success")
            {
                // Cộng ví + đánh dấu success
                await _wallet.CreditAsync(pay.UserId, pay.Amount, $"VNPAY:{txnRef}", referenceId: pay.PaymentId);
                pay.PaymentStatus = "success";
                await _db.SaveChangesAsync();
            }
        }

        var html = $@"<!doctype html><meta charset='utf-8'><title>VNPAY</title>
<body style='font-family:sans-serif;padding:24px'>
  <h3>Thanh toán: {status}</h3>
  <p>Mã đơn: {txnRef}</p>
  <p>Mã phản hồi: {responseCode}</p>
  {(ok && responseCode == "00" ? "<p style='color:green'>✅ Tiền đã được cộng vào ví!</p>" : "")}
</body>";
        return Content(html, "text/html; charset=utf-8");
    }

    // IPN của VNPAY (xác nhận thanh toán server→server, cộng ví ở đây)
    [HttpGet("vnpay/ipn")]
    public async Task<IActionResult> VnpayIpn()
    {
        var ok = _vnp.VerifySignature(Request.Query, out var data);
        if (!ok) return Ok("RspCode=97&Message=Invalid signature");

        var txnRef = data["vnp_TxnRef"];
        var rsp = data.GetValueOrDefault("vnp_ResponseCode");
        var amt100 = long.Parse(data["vnp_Amount"]); // đã *100

        var pay = await _db.Payments.FirstOrDefaultAsync(p => p.InvoiceNumber == txnRef && p.PaymentMethod == "vnpay");
        if (pay is null) return Ok("RspCode=01&Message=Order not found");

        if (pay.PaymentStatus is "success" or "failed") return Ok("RspCode=00&Message=OK");

        if (rsp == "00")
        {
            // cộng ví + đánh dấu success
            await _wallet.CreditAsync(pay.UserId, pay.Amount, $"VNPAY:{txnRef}", referenceId: pay.PaymentId);
            pay.PaymentStatus = "success";
            await _db.SaveChangesAsync();
            return Ok("RspCode=00&Message=OK");
        }
        else
        {
            pay.PaymentStatus = "failed";
            await _db.SaveChangesAsync();
            return Ok("RspCode=00&Message=OK");
        }
    }

    // ---------- MoMo ----------
    [HttpPost("topup/momo/request")]
    public async Task<IActionResult> MoMoRequest([FromBody] WalletTopUpRequestDto req)
    {
        var code = $"TP{Guid.NewGuid():N}".ToUpperInvariant();

        _db.Payments.Add(new PaymentEntity
        {
            UserId = req.UserId,
            Amount = req.Amount,
            PaymentMethod = "momo",
            PaymentStatus = "pending",
            InvoiceNumber = code,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var r = await _momo.CreatePayAsync(code, req.Amount, $"WALLET_TOPUP_{req.UserId}");
        return Ok(new { code, redirect_url = r.PayUrl, deeplink = r.Deeplink });
    }


    // Return của MoMo (cộng ví nếu thành công - chỉ dùng khi IPN không hoạt động)
    [HttpGet("momo/return")]
    public async Task<IActionResult> MoMoReturn()
    {
        // MoMo sẽ redirect với các tham số query như: orderId, resultCode, message, amount, signature (tùy môi trường)
        var orderId = Request.Query["orderId"].ToString();
        var resultCode = Request.Query["resultCode"].ToString();

        // Nếu thanh toán thành công và có orderId, tiến hành cộng ví (fallback khi IPN không dùng được)
        if (!string.IsNullOrEmpty(orderId) && resultCode == "0")
        {
            var pay = await _db.Payments.FirstOrDefaultAsync(p => p.InvoiceNumber == orderId && p.PaymentMethod == "momo");
            if (pay != null && pay.PaymentStatus != "success")
            {
                await _wallet.CreditAsync(pay.UserId, pay.Amount, $"MOMO:{orderId}", referenceId: pay.PaymentId);
                pay.PaymentStatus = "success";
                await _db.SaveChangesAsync();
            }
        }

        var html = "<!doctype html><meta charset='utf-8'><body style='font-family:sans-serif;padding:24px'>" +
                   (resultCode == "0" ? "<h3>Thanh toán THÀNH CÔNG</h3><p>✅ Tiền đã được cộng vào ví!</p>"
                                        : "<h3>Đã xử lý thanh toán. Bạn có thể đóng cửa sổ.</h3>") +
                   "</body>";
        return Content(html, "text/html; charset=utf-8");
    }

    // IPN của MoMo (POST JSON)
    [HttpPost("momo/ipn")]
    public async Task<IActionResult> MoMoIpn([FromBody] Dictionary<string, string> form)
    {
        if (!_momo.VerifyIpnSignature(form)) return Ok(new { resultCode = 97, message = "Invalid signature" });

        var orderId = form["orderId"];
        var resultCode = form["resultCode"];
        var amount = decimal.Parse(form["amount"]);

        var pay = await _db.Payments.FirstOrDefaultAsync(p => p.InvoiceNumber == orderId && p.PaymentMethod == "momo");
        if (pay is null) return Ok(new { resultCode = 01, message = "Order not found" });

        if (pay.PaymentStatus is "success" or "failed")
            return Ok(new { resultCode = 0, message = "OK" });

        if (resultCode == "0")
        {
            await _wallet.CreditAsync(pay.UserId, pay.Amount, $"MOMO:{orderId}", referenceId: pay.PaymentId);
            pay.PaymentStatus = "success";
            await _db.SaveChangesAsync();
        }
        else
        {
            pay.PaymentStatus = "failed";
            await _db.SaveChangesAsync();
        }

        return Ok(new { resultCode = 0, message = "Confirm Success" });
    }
}
