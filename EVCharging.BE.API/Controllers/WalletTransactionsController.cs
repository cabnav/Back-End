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
    private readonly EvchargingManagementContext _db;
    private readonly IWalletService _wallet;

    public WalletTransactionsController(IMockPayService mock,
                                        EvchargingManagementContext db, IWalletService wallet)
    {
        _mock = mock; _db = db; _wallet = wallet;
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
}

