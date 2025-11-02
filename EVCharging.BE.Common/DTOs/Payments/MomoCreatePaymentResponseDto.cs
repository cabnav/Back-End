using System.Text.Json.Serialization;

namespace EVCharging.BE.Common.DTOs.Payments
{
    /// <summary>
    /// DTO phản hồi từ MoMo khi tạo payment request
    /// </summary>
    public class MomoCreatePaymentResponseDto
    {
        [JsonPropertyName("requestId")]
        public string RequestId { get; set; } = string.Empty;

        [JsonPropertyName("errorCode")]
        public int ErrorCode { get; set; }

        [JsonPropertyName("orderId")]
        public string OrderId { get; set; } = string.Empty;

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("localMessage")]
        public string LocalMessage { get; set; } = string.Empty;

        [JsonPropertyName("requestType")]
        public string RequestType { get; set; } = string.Empty;

        [JsonPropertyName("payUrl")]
        public string PayUrl { get; set; } = string.Empty;

        [JsonPropertyName("signature")]
        public string Signature { get; set; } = string.Empty;

        [JsonPropertyName("qrCodeUrl")]
        public string QrCodeUrl { get; set; } = string.Empty;

        [JsonPropertyName("deeplink")]
        public string Deeplink { get; set; } = string.Empty;

        [JsonPropertyName("deeplinkWebInApp")]
        public string DeeplinkWebInApp { get; set; } = string.Empty;
    }
}

