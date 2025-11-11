using EVCharging.BE.Common.DTOs.Subscriptions;
using EVCharging.BE.DAL;
using EVCharging.BE.DAL.Entities;
using EVCharging.BE.Services.Services.Payment;
using EVCharging.BE.Services.Services.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace EVCharging.BE.Services.Services.Subscriptions.Implementations
{
    public class SubscriptionService : ISubscriptionService
    {
        private readonly EvchargingManagementContext _db;
        private readonly IWalletService _walletService;
        private readonly IMockPayService _mockPayService;
        private readonly IMomoService _momoService;
        private readonly IPricingPlanService _pricingPlanService;

        public SubscriptionService(
            EvchargingManagementContext db,
            IWalletService walletService,
            IMockPayService mockPayService,
            IMomoService momoService,
            IPricingPlanService pricingPlanService)
        {
            _db = db;
            _walletService = walletService;
            _mockPayService = mockPayService;
            _momoService = momoService;
            _pricingPlanService = pricingPlanService;
        }

        public async Task<SubscriptionResponse> SubscribeAsync(int userId, SubscribeRequest request)
        {
            // Lấy plan từ database theo tên (Tier)
            var plan = await _pricingPlanService.GetPlanByNameAsync(request.Tier);
            if (plan == null || plan.IsActive != true)
                throw new ArgumentException($"Pricing plan '{request.Tier}' not found or inactive");

            // Validate billing cycle
            if (plan.BillingCycle?.ToLower() != request.BillingCycle.ToLower())
                throw new ArgumentException($"Billing cycle '{request.BillingCycle}' does not match plan's billing cycle '{plan.BillingCycle}'");

            // Get price
            var price = plan.Price;

            // Get user
            var user = await _db.Users.FindAsync(userId);
            if (user == null)
                throw new KeyNotFoundException("User not found");

            // Xử lý thanh toán
            if (request.PaymentMethod.ToLower() == "wallet")
            {
                // Trừ tiền từ ví
                var balance = await _walletService.GetBalanceAsync(userId);
                if (balance < price)
                    throw new InvalidOperationException($"Insufficient wallet balance. Required: {price:N0} VND, Current: {balance:N0} VND");

                await _walletService.DebitAsync(
                    userId,
                    price,
                    $"Thanh toán gói {request.Tier.ToUpper()} - {request.BillingCycle}",
                    null
                );
            }
            else if (request.PaymentMethod.ToLower() == "momo" || request.PaymentMethod.ToLower() == "mock")
            {
                // TODO: Tạo payment request cho MoMo/MockPay
                // Tạm thời throw exception
                throw new NotImplementedException($"Payment method {request.PaymentMethod} not implemented yet");
            }

            // Update user MembershipTier (dùng Name của plan)
            user.MembershipTier = plan.Name.ToLower();

            // Tính EndDate dựa trên BillingCycle
            var startDate = DateTime.UtcNow;
            var endDate = request.BillingCycle.ToLower() == "monthly"
                ? startDate.AddMonths(1)
                : startDate.AddYears(1);

            // Lưu thay đổi MembershipTier
            await _db.SaveChangesAsync();

            // Note: Không tạo Subscription record để tránh lỗi foreign key với PricingPlan
            // Chỉ dùng User.MembershipTier để track subscription

            return new SubscriptionResponse
            {
                Tier = plan.Name.ToLower(),
                DiscountRate = plan.DiscountRate ?? 0m,
                StartDate = startDate,
                EndDate = endDate,
                IsActive = true,
                BillingCycle = request.BillingCycle.ToLower(),
                Price = price
            };
        }

        public async Task<SubscriptionResponse?> GetActiveSubscriptionAsync(int userId)
        {
            var user = await _db.Users.FindAsync(userId);
            if (user == null || string.IsNullOrEmpty(user.MembershipTier))
                return null;

            // Lấy plan từ database
            var plan = await _pricingPlanService.GetPlanByNameAsync(user.MembershipTier);
            if (plan == null || plan.IsActive != true)
                return null;

            return new SubscriptionResponse
            {
                Tier = plan.Name.ToLower(),
                DiscountRate = plan.DiscountRate ?? 0m,
                StartDate = null, // Không có record
                EndDate = null,   // Không có record
                IsActive = true,
                BillingCycle = plan.BillingCycle
            };
        }

        public async Task<bool> CancelSubscriptionAsync(int userId)
        {
            var user = await _db.Users.FindAsync(userId);
            if (user == null)
                return false;

            // Reset MembershipTier
            user.MembershipTier = null;

            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<decimal> GetDiscountRateAsync(int userId)
        {
            var user = await _db.Users.FindAsync(userId);
            if (user == null || string.IsNullOrEmpty(user.MembershipTier))
                return 0m;

            // Lấy discount rate từ database
            return await _pricingPlanService.GetDiscountRateByNameAsync(user.MembershipTier);
        }
    }
}

