using EVCharging.BE.DAL.Models;
using EVCharging.BE.DAL.Models.EVChargingManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace EVCharging.BE.DAL.Models
{
    public class EVChargingDbContext : DbContext
    {
        public EVChargingDbContext(DbContextOptions<EVChargingDbContext> options) : base(options) { }

        // DbSets với tên khớp hoàn toàn
        public DbSet<User> User { get; set; }
        public DbSet<CorporateAccount> CorporateAccount { get; set; }
        public DbSet<DriverProfile> DriverProfile { get; set; }
        public DbSet<ChargingStation> ChargingStation { get; set; }
        public DbSet<ChargingPoint> ChargingPoint { get; set; }
        public DbSet<PricingPlan> PricingPlan { get; set; }
        public DbSet<Subscription> Subscription { get; set; }
        public DbSet<Reservation> Reservation { get; set; }
        public DbSet<ChargingSession> ChargingSession { get; set; }
        public DbSet<Payment> Payment { get; set; }
        public DbSet<WalletTransaction> WalletTransaction { get; set; }
        public DbSet<StationStaff> StationStaff { get; set; }
        public DbSet<IncidentReport> IncidentReport { get; set; }
        public DbSet<SessionLog> SessionLog { get; set; }
        public DbSet<Notification> Notification { get; set; }
        public DbSet<Invoice> Invoice { get; set; }
        public DbSet<InvoiceItem> InvoiceItem { get; set; }
        public DbSet<UsageAnalytics> UsageAnalytics { get; set; }
        public DbSet<BillingPlan> BillingPlan { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Cấu hình quan hệ và ràng buộc
            modelBuilder.Entity<Subscription>()
                .HasCheckConstraint("CK_Subscription_UserOrCorporate",
                    "user_id IS NOT NULL OR corporate_id IS NOT NULL");

            modelBuilder.Entity<Invoice>()
                .HasCheckConstraint("CK_Invoice_UserOrCorporate",
                    "user_id IS NOT NULL OR corporate_id IS NOT NULL");

            // Cấu hình unique constraints
            modelBuilder.Entity<User>()
                .HasIndex(u => u.email)
                .IsUnique();

            modelBuilder.Entity<ChargingPoint>()
                .HasIndex(cp => cp.qr_code)
                .IsUnique();

            modelBuilder.Entity<Invoice>()
                .HasIndex(i => i.invoice_number)
                .IsUnique();

            base.OnModelCreating(modelBuilder);
        }
    }
}