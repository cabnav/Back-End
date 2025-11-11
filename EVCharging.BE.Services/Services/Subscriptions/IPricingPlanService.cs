using EVCharging.BE.Common.DTOs.Subscriptions;

namespace EVCharging.BE.Services.Services.Subscriptions
{
    public interface IPricingPlanService
    {
        /// <summary>
        /// Lấy tất cả pricing plans (active hoặc all)
        /// </summary>
        Task<IEnumerable<PricingPlanDTO>> GetAllPlansAsync(bool activeOnly = true);

        /// <summary>
        /// Lấy pricing plan theo ID
        /// </summary>
        Task<PricingPlanDTO?> GetPlanByIdAsync(int planId);

        /// <summary>
        /// Lấy pricing plan theo tên
        /// </summary>
        Task<PricingPlanDTO?> GetPlanByNameAsync(string name);

        /// <summary>
        /// Tạo pricing plan mới
        /// </summary>
        Task<PricingPlanDTO> CreatePlanAsync(PricingPlanCreateRequest request);

        /// <summary>
        /// Cập nhật pricing plan
        /// </summary>
        Task<PricingPlanDTO?> UpdatePlanAsync(int planId, PricingPlanUpdateRequest request);

        /// <summary>
        /// Xóa/deactivate pricing plan
        /// </summary>
        Task<bool> DeactivatePlanAsync(int planId);

        /// <summary>
        /// Lấy discount rate theo plan name (từ database)
        /// </summary>
        Task<decimal> GetDiscountRateByNameAsync(string planName);
    }
}

