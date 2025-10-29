namespace EVCharging.BE.Services.Services.Payment
{
    public record MoMoCreateResult(string PayUrl, string? Deeplink);

    public interface IMoMoService
    {
        // wrapper cũ (giữ lại để tương thích)
        Task<string> CreatePayUrlAsync(string orderId, decimal amount, string orderInfo);

        // ✅ mới: trả cả deeplink
        Task<MoMoCreateResult> CreatePayAsync(string orderId, decimal amount, string orderInfo);

        bool VerifyIpnSignature(Dictionary<string, string> form);
    }
}
