using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EVCharging.BE.DAL.Models
{
    using Microsoft.EntityFrameworkCore;

    public class EVChargingDbContext : DbContext
    {
        public EVChargingDbContext(DbContextOptions<EVChargingDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<DriverProfile> DriverProfiles { get; set; }
        public DbSet<CorporateAccount> CorporateAccounts { get; set; }
        public DbSet<ChargingStation> ChargingStations { get; set; }
        public DbSet<ChargingPoint> ChargingPoints { get; set; }
        public DbSet<PricingPlan> PricingPlans { get; set; }
        public DbSet<Subscription> Subscriptions { get; set; }
        public DbSet<Reservation> Reservations { get; set; }
        public DbSet<ChargingSession> ChargingSessions { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<WalletTransaction> WalletTransactions { get; set; }
        public DbSet<StationStaff> StationStaffs { get; set; }
        public DbSet<IncidentReport> IncidentReports { get; set; }
        public DbSet<SessionLog> SessionLogs { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<InvoiceItem> InvoiceItems { get; set; }
        public DbSet<UsageAnalytics> UsageAnalytics { get; set; }
        public DbSet<BillingPlan> BillingPlans { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configure relationships and constraints
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<ChargingPoint>()
                .HasIndex(cp => cp.QrCode)
                .IsUnique();

            modelBuilder.Entity<Invoice>()
                .HasIndex(i => i.InvoiceNumber)
                .IsUnique();

            // Additional configuration can be added here
        }
    }
}
