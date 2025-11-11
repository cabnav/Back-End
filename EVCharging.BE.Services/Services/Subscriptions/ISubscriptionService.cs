using EVCharging.BE.Common.DTOs.Subscriptions;

namespace EVCharging.BE.Services.Services.Subscriptions
{
    public interface ISubscriptionService
    {
        /// <summary>
        /// Đăng ký gói subscription cho user
        /// </summary>
        Task<SubscriptionResponse> SubscribeAsync(int userId, SubscribeRequest request);

        /// <summary>
        /// Lấy subscription đang active của user
        /// </summary>
        Task<SubscriptionResponse?> GetActiveSubscriptionAsync(int userId);

        /// <summary>
        /// Hủy subscription của user
        /// </summary>
        Task<bool> CancelSubscriptionAsync(int userId);

        /// <summary>
        /// Lấy discount rate của user (từ MembershipTier)
        /// </summary>
        Task<decimal> GetDiscountRateAsync(int userId);
    }
}

