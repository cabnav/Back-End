using EVCharging.BE.Common.DTOs.Subscriptions;
using EVCharging.BE.Common.DTOs.Payments;
using EVCharging.BE.DAL;
using EVCharging.BE.DAL.Entities;
using EVCharging.BE.Services.Services.Payment;
using EVCharging.BE.Services.Services.Subscriptions;
using Microsoft.EntityFrameworkCore;
using PaymentEntity = EVCharging.BE.DAL.Entities.Payment;

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
                // Kiểm tra số dư ví
                var balance = await _walletService.GetBalanceAsync(userId);
                if (balance < price)
                    throw new InvalidOperationException($"Insufficient wallet balance. Required: {price:N0} VND, Current: {balance:N0} VND");

                // Tạo Payment record để tracking lịch sử thanh toán
                var orderId = $"SUB_{userId}_{DateTime.UtcNow:yyyyMMddHHmmss}";
                var payment = new PaymentEntity
                {
                    UserId = userId,
                    Amount = price,
                    PaymentMethod = "wallet",
                    PaymentStatus = "success", // Thanh toán thành công ngay
                    PaymentType = "subscription", // Đánh dấu đây là subscription payment
                    InvoiceNumber = orderId,
                    CreatedAt = DateTime.UtcNow
                };

                _db.Payments.Add(payment);
                await _db.SaveChangesAsync();

                // Trừ tiền từ ví (sau khi tạo payment record)
                await _walletService.DebitAsync(
                    userId,
                    price,
                    $"Thanh toán gói {request.Tier.ToUpper()} - {request.BillingCycle}",
                    payment.PaymentId // Link với payment record
                );

                // Update user MembershipTier ngay lập tức (dùng Name của plan)
                user.MembershipTier = plan.Name.ToLower();

                // Tính EndDate dựa trên BillingCycle
                var startDate = DateTime.UtcNow;
                var endDate = request.BillingCycle.ToLower() == "monthly"
                    ? startDate.AddMonths(1)
                    : startDate.AddYears(1);

                // Lưu thay đổi MembershipTier
                await _db.SaveChangesAsync();

                return new SubscriptionResponse
                {
                    Tier = plan.Name.ToLower(),
                    DiscountRate = plan.DiscountRate ?? 0m,
                    StartDate = startDate,
                    EndDate = endDate,
                    IsActive = true,
                    BillingCycle = request.BillingCycle.ToLower(),
                    Price = price,
                    PaymentUrl = null // Không cần payment URL cho wallet
                };
            }
            else if (request.PaymentMethod.ToLower() == "momo")
            {
                // Tạo Payment record với status "pending"
                var orderId = $"SUB_{userId}_{DateTime.UtcNow:yyyyMMddHHmmss}";
                var payment = new PaymentEntity
                {
                    UserId = userId,
                    Amount = price,
                    PaymentMethod = "momo",
                    PaymentStatus = "pending",
                    PaymentType = "subscription", // Đánh dấu đây là subscription payment
                    InvoiceNumber = orderId,
                    CreatedAt = DateTime.UtcNow
                };

                _db.Payments.Add(payment);
                await _db.SaveChangesAsync();

                // Tạo MoMo payment request
                var fullName = user.Name ?? "Khách hàng";
                var momoRequest = new MomoCreatePaymentRequestDto
                {
                    SessionId = 0, // Không có session, đây là subscription payment
                    UserId = userId,
                    Amount = price,
                    FullName = fullName,
                    OrderInfo = $"Đăng ký gói {request.Tier.ToUpper()} - {request.BillingCycle}|TIER:{request.Tier}|BILLING:{request.BillingCycle}" // Lưu thông tin subscription vào OrderInfo để parse lại sau
                };

                var momoResponse = await _momoService.CreatePaymentAsync(momoRequest);

                // Cập nhật Payment với orderId từ MoMo
                payment.InvoiceNumber = momoResponse.OrderId;
                await _db.SaveChangesAsync();

                if (momoResponse.ErrorCode != 0)
                {
                    // Nếu có lỗi, xóa payment record và trả về lỗi
                    _db.Payments.Remove(payment);
                    await _db.SaveChangesAsync();

                    throw new InvalidOperationException($"MoMo payment creation failed: {momoResponse.Message}");
                }

                // Trả về response với payment URL (subscription sẽ được kích hoạt sau khi thanh toán thành công)
                return new SubscriptionResponse
                {
                    Tier = plan.Name.ToLower(),
                    DiscountRate = plan.DiscountRate ?? 0m,
                    StartDate = null, // Sẽ được set sau khi thanh toán thành công
                    EndDate = null,   // Sẽ được set sau khi thanh toán thành công
                    IsActive = false, // Chưa active, đợi thanh toán
                    BillingCycle = request.BillingCycle.ToLower(),
                    Price = price,
                    PaymentUrl = momoResponse.PayUrl // URL để redirect user đến trang thanh toán MoMo
                };
            }
            else if (request.PaymentMethod.ToLower() == "mock")
            {
                throw new NotImplementedException($"Payment method {request.PaymentMethod} not implemented yet");
            }
            else
            {
                throw new ArgumentException($"Invalid payment method: {request.PaymentMethod}");
            }
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

        public async Task<bool> ActivateSubscriptionFromPaymentAsync(int userId, string tier, string billingCycle)
        {
            try
            {
                var user = await _db.Users.FindAsync(userId);
                if (user == null)
                    return false;

                // Lấy plan từ database
                var plan = await _pricingPlanService.GetPlanByNameAsync(tier);
                if (plan == null || plan.IsActive != true)
                    return false;

                // Update user MembershipTier
                user.MembershipTier = plan.Name.ToLower();
                await _db.SaveChangesAsync();

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}

