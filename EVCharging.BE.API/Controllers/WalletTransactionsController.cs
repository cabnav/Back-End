using EVCharging.BE.Common.DTOs.Payments;
using System.Text;
using QRCoder;
using Microsoft.AspNetCore.Mvc;
using EVCharging.BE.Services.Services.Payment;

[ApiController]
[Route("api/[controller]")]
public class WalletTransactionsController : ControllerBase
{
    private readonly IMockPayService _mock;
    public WalletTransactionsController(IMockPayService mock) => _mock = mock;

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

        var html = $@"<!doctype html>
<html lang='vi'><head><meta charset='utf-8'><title>Kết quả thanh toán</title></head>
<body style='font-family:sans-serif;padding:24px'>
  <h3>Kết quả: {(success ? "THÀNH CÔNG" : "THẤT BẠI")}</h3>
  <p>Mã GD: {code}</p>
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