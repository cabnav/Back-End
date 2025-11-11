using EVCharging.BE.Common.DTOs.Subscriptions;
using EVCharging.BE.DAL;
using EVCharging.BE.DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace EVCharging.BE.Services.Services.Subscriptions.Implementations
{
    public class PricingPlanService : IPricingPlanService
    {
        private readonly EvchargingManagementContext _db;

        public PricingPlanService(EvchargingManagementContext db)
        {
            _db = db;
        }

        public async Task<IEnumerable<PricingPlanDTO>> GetAllPlansAsync(bool activeOnly = true)
        {
            var query = _db.PricingPlans.AsQueryable();

            if (activeOnly)
            {
                query = query.Where(p => p.IsActive == true);
            }

            var plans = await query
                .OrderBy(p => p.Price)
                .ToListAsync();

            return plans.Select(MapToDTO);
        }

        public async Task<PricingPlanDTO?> GetPlanByIdAsync(int planId)
        {
            var plan = await _db.PricingPlans.FindAsync(planId);
            return plan == null ? null : MapToDTO(plan);
        }

        public async Task<PricingPlanDTO?> GetPlanByNameAsync(string name)
        {
            var plan = await _db.PricingPlans
                .FirstOrDefaultAsync(p => p.Name.ToLower() == name.ToLower());
            return plan == null ? null : MapToDTO(plan);
        }

        public async Task<PricingPlanDTO> CreatePlanAsync(PricingPlanCreateRequest request)
        {
            // Check duplicate name
            var existing = await _db.PricingPlans
                .AnyAsync(p => p.Name.ToLower() == request.Name.ToLower());
            if (existing)
                throw new InvalidOperationException($"Pricing plan with name '{request.Name}' already exists");

            var plan = new PricingPlan
            {
                Name = request.Name,
                PlanType = request.PlanType ?? "subscription",
                Description = request.Description,
                Price = request.Price,
                BillingCycle = request.BillingCycle,
                DiscountRate = request.DiscountRate,
                TargetAudience = request.TargetAudience ?? "user",
                Benefits = request.Benefits,
                IsActive = request.IsActive
            };

            _db.PricingPlans.Add(plan);
            await _db.SaveChangesAsync();

            return MapToDTO(plan);
        }

        public async Task<PricingPlanDTO?> UpdatePlanAsync(int planId, PricingPlanUpdateRequest request)
        {
            var plan = await _db.PricingPlans.FindAsync(planId);
            if (plan == null)
                return null;

            // Check duplicate name nếu đổi tên
            if (!string.IsNullOrEmpty(request.Name) && request.Name.ToLower() != plan.Name.ToLower())
            {
                var existing = await _db.PricingPlans
                    .AnyAsync(p => p.Name.ToLower() == request.Name.ToLower() && p.PlanId != planId);
                if (existing)
                    throw new InvalidOperationException($"Pricing plan with name '{request.Name}' already exists");
            }

            // Update fields
            if (!string.IsNullOrEmpty(request.Name))
                plan.Name = request.Name;
            if (request.PlanType != null)
                plan.PlanType = request.PlanType;
            if (request.Description != null)
                plan.Description = request.Description;
            if (request.Price.HasValue)
                plan.Price = request.Price.Value;
            if (!string.IsNullOrEmpty(request.BillingCycle))
                plan.BillingCycle = request.BillingCycle;
            if (request.DiscountRate.HasValue)
                plan.DiscountRate = request.DiscountRate.Value;
            if (request.TargetAudience != null)
                plan.TargetAudience = request.TargetAudience;
            if (request.Benefits != null)
                plan.Benefits = request.Benefits;
            if (request.IsActive.HasValue)
                plan.IsActive = request.IsActive.Value;

            await _db.SaveChangesAsync();

            return MapToDTO(plan);
        }

        public async Task<bool> DeactivatePlanAsync(int planId)
        {
            var plan = await _db.PricingPlans.FindAsync(planId);
            if (plan == null)
                return false;

            plan.IsActive = false;
            await _db.SaveChangesAsync();

            return true;
        }

        public async Task<decimal> GetDiscountRateByNameAsync(string planName)
        {
            var plan = await _db.PricingPlans
                .Where(p => p.Name.ToLower() == planName.ToLower() && p.IsActive == true)
                .FirstOrDefaultAsync();

            return plan?.DiscountRate ?? 0m;
        }

        private static PricingPlanDTO MapToDTO(PricingPlan plan)
        {
            return new PricingPlanDTO
            {
                PlanId = plan.PlanId,
                Name = plan.Name,
                PlanType = plan.PlanType,
                Description = plan.Description,
                Price = plan.Price,
                BillingCycle = plan.BillingCycle,
                DiscountRate = plan.DiscountRate,
                TargetAudience = plan.TargetAudience,
                Benefits = plan.Benefits,
                IsActive = plan.IsActive
            };
        }
    }
}

