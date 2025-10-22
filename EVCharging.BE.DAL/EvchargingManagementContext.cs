using EVCharging.BE.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;

namespace EVCharging.BE.DAL;

public partial class EvchargingManagementContext : DbContext
{
    public EvchargingManagementContext()
    {
    }

    public EvchargingManagementContext(DbContextOptions<EvchargingManagementContext> options)
        : base(options)
    {
    }

    public virtual DbSet<BillingPlan> BillingPlans { get; set; }

    public virtual DbSet<ChargingPoint> ChargingPoints { get; set; }

    public virtual DbSet<ChargingSession> ChargingSessions { get; set; }

    public virtual DbSet<ChargingStation> ChargingStations { get; set; }

    public virtual DbSet<CorporateAccount> CorporateAccounts { get; set; }

    public virtual DbSet<DriverProfile> DriverProfiles { get; set; }

    public virtual DbSet<IncidentReport> IncidentReports { get; set; }

    public virtual DbSet<Invoice> Invoices { get; set; }

    public virtual DbSet<InvoiceItem> InvoiceItems { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<PasswordResetToken> PasswordResetTokens { get; set; }

    public virtual DbSet<Payment> Payments { get; set; }

    public virtual DbSet<PricingPlan> PricingPlans { get; set; }

    public virtual DbSet<Reservation> Reservations { get; set; }

    public virtual DbSet<SessionLog> SessionLogs { get; set; }

    public virtual DbSet<StationStaff> StationStaffs { get; set; }

    public virtual DbSet<Subscription> Subscriptions { get; set; }

    public virtual DbSet<UsageAnalytic> UsageAnalytics { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserBilling> UserBillings { get; set; }

    public virtual DbSet<WalletTransaction> WalletTransactions { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlServer(GetConnectionString());

    }
    private string GetConnectionString()
    {
        IConfiguration config = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", true, true)
                    .Build();
        var strConn = config["ConnectionStrings:DefaultConnection"];

        return strConn ?? throw new InvalidOperationException("Connection string not found");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BillingPlan>(entity =>
        {
            entity.HasKey(e => e.PlanId).HasName("PK__BillingP__BE9F8F1DF705AFAA");

            entity.ToTable("BillingPlan");

            entity.Property(e => e.PlanId).HasColumnName("plan_id");
            entity.Property(e => e.BillingCycle)
                .HasMaxLength(20)
                .HasColumnName("billing_cycle");
            entity.Property(e => e.CreditLimit)
                .HasDefaultValue(0.00m)
.HasColumnType("decimal(10, 2)")
                .HasColumnName("credit_limit");
            entity.Property(e => e.PaymentTerms)
                .HasMaxLength(20)
                .HasColumnName("payment_terms");
            entity.Property(e => e.PlanName)
                .HasMaxLength(100)
                .HasColumnName("plan_name");
            entity.Property(e => e.SubscriptionFee)
                .HasDefaultValue(0.00m)
                .HasColumnType("decimal(10, 2)")
                .HasColumnName("subscription_fee");
        });

        modelBuilder.Entity<ChargingPoint>(entity =>
        {
            entity.HasKey(e => e.PointId).HasName("PK__Charging__0241361278E8E343");

            entity.ToTable("ChargingPoint");

            entity.HasIndex(e => e.QrCode, "UQ__Charging__E2FB8889AB2F26BA").IsUnique();

            entity.Property(e => e.PointId).HasColumnName("point_id");
            entity.Property(e => e.ConnectorType)
                .HasMaxLength(20)
                .HasColumnName("connector_type");
            entity.Property(e => e.CurrentPower)
                .HasDefaultValue(0.0)
                .HasColumnName("current_power");
            entity.Property(e => e.LastMaintenance).HasColumnName("last_maintenance");
            entity.Property(e => e.PowerOutput).HasColumnName("power_output");
            entity.Property(e => e.PricePerKwh)
                .HasColumnType("decimal(8, 2)")
                .HasColumnName("price_per_kwh");
            entity.Property(e => e.QrCode)
                .HasMaxLength(100)
                .HasColumnName("qr_code");
            entity.Property(e => e.StationId).HasColumnName("station_id");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("available")
                .HasColumnName("status");

            entity.HasOne(d => d.Station).WithMany(p => p.ChargingPoints)
                .HasForeignKey(d => d.StationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ChargingP__stati__5629CD9C");
        });

        modelBuilder.Entity<ChargingSession>(entity =>
        {
            entity.HasKey(e => e.SessionId).HasName("PK__Charging__69B13FDCD27EB038");

            entity.ToTable("ChargingSession");

            entity.Property(e => e.SessionId).HasColumnName("session_id");
            entity.Property(e => e.AppliedDiscount)
                .HasDefaultValue(0.00m)
                .HasColumnType("decimal(10, 2)")
                .HasColumnName("applied_discount");
            entity.Property(e => e.CostBeforeDiscount)
                .HasDefaultValue(0.00m)
                .HasColumnType("decimal(10, 2)")
                .HasColumnName("cost_before_discount");
            entity.Property(e => e.DriverId).HasColumnName("driver_id");
            entity.Property(e => e.DurationMinutes)
                .HasDefaultValue(0)
                .HasColumnName("duration_minutes");
            entity.Property(e => e.EndTime).HasColumnName("end_time");
            entity.Property(e => e.EnergyUsed)
                .HasDefaultValue(0.00m)
                .HasColumnType("decimal(8, 2)")
                .HasColumnName("energy_used");
            entity.Property(e => e.FinalCost)
                .HasDefaultValue(0.00m)
                .HasColumnType("decimal(10, 2)")
                .HasColumnName("final_cost");
            entity.Property(e => e.FinalSoc).HasColumnName("final_soc");
            entity.Property(e => e.InitialSoc).HasColumnName("initial_soc");
            entity.Property(e => e.PointId).HasColumnName("point_id");
            entity.Property(e => e.StartTime).HasColumnName("start_time");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("in_progress")
                .HasColumnName("status");

            entity.HasOne(d => d.Driver).WithMany(p => p.ChargingSessions)
                .HasForeignKey(d => d.DriverId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ChargingS__drive__787EE5A0");

            entity.HasOne(d => d.Point).WithMany(p => p.ChargingSessions)
                .HasForeignKey(d => d.PointId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ChargingS__point__797309D9");
        });

        modelBuilder.Entity<ChargingStation>(entity =>
        {
            entity.HasKey(e => e.StationId).HasName("PK__Charging__44B370E9E6D6BED2");

            entity.ToTable("ChargingStation");

            entity.Property(e => e.StationId).HasColumnName("station_id");
            entity.Property(e => e.Address)
                .HasMaxLength(500)
                .HasColumnName("address");
            entity.Property(e => e.AvailablePoints)
                .HasDefaultValue(0)
                .HasColumnName("available_points");
            entity.Property(e => e.Latitude).HasColumnName("latitude");
            entity.Property(e => e.Longitude).HasColumnName("longitude");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");
            entity.Property(e => e.Operator)
                .HasMaxLength(100)
                .HasColumnName("operator");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("active")
                .HasColumnName("status");
            entity.Property(e => e.TotalPoints)
                .HasDefaultValue(0)
                .HasColumnName("total_points");
        });

        modelBuilder.Entity<CorporateAccount>(entity =>
        {
            entity.HasKey(e => e.CorporateId).HasName("PK__Corporat__869F887FBE85D258");

            entity.ToTable("CorporateAccount");

            entity.Property(e => e.CorporateId).HasColumnName("corporate_id");
            entity.Property(e => e.AdminUserId).HasColumnName("admin_user_id");
            entity.Property(e => e.BillingType)
                            .HasMaxLength(20)
                            .HasColumnName("billing_type");
            entity.Property(e => e.CompanyName)
                .HasMaxLength(200)
                .HasColumnName("company_name");
            entity.Property(e => e.ContactEmail)
                .HasMaxLength(255)
                .HasColumnName("contact_email");
            entity.Property(e => e.ContactPerson)
                .HasMaxLength(100)
                .HasColumnName("contact_person");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreditLimit)
                .HasDefaultValue(0.00m)
                .HasColumnType("decimal(10, 2)")
                .HasColumnName("credit_limit");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("active")
                .HasColumnName("status");
            entity.Property(e => e.TaxCode)
                .HasMaxLength(50)
                .HasColumnName("tax_code");

            entity.HasOne(d => d.AdminUser).WithMany(p => p.CorporateAccounts)
                .HasForeignKey(d => d.AdminUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Corporate__admin__440B1D61");
        });

        modelBuilder.Entity<DriverProfile>(entity =>
        {
            entity.HasKey(e => e.DriverId).HasName("PK__DriverPr__A411C5BDF3DEAD50");

            entity.ToTable("DriverProfile");

            entity.HasIndex(e => e.UserId, "UQ__DriverPr__B9BE370E9658F869").IsUnique();

            entity.Property(e => e.DriverId).HasColumnName("driver_id");
            entity.Property(e => e.BatteryCapacity).HasColumnName("battery_capacity");
            entity.Property(e => e.CorporateId).HasColumnName("corporate_id");
            entity.Property(e => e.LicenseNumber)
                .HasMaxLength(50)
                .HasColumnName("license_number");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.VehicleModel)
                .HasMaxLength(100)
                .HasColumnName("vehicle_model");
            entity.Property(e => e.VehiclePlate)
                .HasMaxLength(20)
                .HasColumnName("vehicle_plate");

            entity.HasOne(d => d.Corporate).WithMany(p => p.DriverProfiles)
                .HasForeignKey(d => d.CorporateId)
                .HasConstraintName("FK__DriverPro__corpo__48CFD27E");

            entity.HasOne(d => d.User).WithOne(p => p.DriverProfile)
                .HasForeignKey<DriverProfile>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__DriverPro__user___47DBAE45");
        });

        modelBuilder.Entity<IncidentReport>(entity =>
        {
            entity.HasKey(e => e.ReportId).HasName("PK__Incident__779B7C58C49F8DD7");

            entity.ToTable("IncidentReport");

            entity.Property(e => e.ReportId).HasColumnName("report_id");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.PointId).HasColumnName("point_id");
            entity.Property(e => e.Priority)
                .HasMaxLength(20)
                .HasDefaultValue("medium")
                .HasColumnName("priority");
            entity.Property(e => e.ReportedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("reported_at");
            entity.Property(e => e.ReporterId).HasColumnName("reporter_id");
            entity.Property(e => e.ResolvedAt).HasColumnName("resolved_at");
            entity.Property(e => e.ResolvedBy).HasColumnName("resolved_by");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("pending")
                .HasColumnName("status");
            entity.Property(e => e.Title)
                .HasMaxLength(200)
                .HasColumnName("title");

            entity.HasOne(d => d.Point).WithMany(p => p.IncidentReports)
                .HasForeignKey(d => d.PointId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__IncidentR__point__160F4887");

            entity.HasOne(d => d.Reporter).WithMany(p => p.IncidentReportReporters)
                .HasForeignKey(d => d.ReporterId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__IncidentR__repor__151B244E");

            entity.HasOne(d => d.ResolvedByNavigation).WithMany(p => p.IncidentReportResolvedByNavigations)
                .HasForeignKey(d => d.ResolvedBy)
                .HasConstraintName("FK__IncidentR__resol__17036CC0");
        });

        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.HasKey(e => e.InvoiceId).HasName("PK__Invoice__F58DFD4908A88F94");

            entity.ToTable("Invoice");

            entity.HasIndex(e => e.InvoiceNumber, "UQ__Invoice__8081A63A881D93DE").IsUnique();

            entity.Property(e => e.InvoiceId).HasColumnName("invoice_id");
            entity.Property(e => e.BillingPeriodEnd).HasColumnName("billing_period_end");
            entity.Property(e => e.BillingPeriodStart).HasColumnName("billing_period_start");
            entity.Property(e => e.CorporateId).HasColumnName("corporate_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("created_at");
            entity.Property(e => e.DueDate).HasColumnName("due_date");
            entity.Property(e => e.InvoiceNumber)
                .HasMaxLength(50)
                .HasColumnName("invoice_number");
            entity.Property(e => e.PaidAt).HasColumnName("paid_at");
            entity.Property(e => e.Status)
.HasMaxLength(20)
                .HasDefaultValue("draft")
                .HasColumnName("status");
            entity.Property(e => e.TotalAmount)
                .HasColumnType("decimal(10, 2)")
                .HasColumnName("total_amount");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Corporate).WithMany(p => p.Invoices)
                .HasForeignKey(d => d.CorporateId)
                .HasConstraintName("FK__Invoice__corpora__2BFE89A6");

            entity.HasOne(d => d.User).WithMany(p => p.Invoices)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK__Invoice__user_id__2B0A656D");
        });

        modelBuilder.Entity<InvoiceItem>(entity =>
        {
            entity.HasKey(e => e.ItemId).HasName("PK__InvoiceI__52020FDD4B832DAF");

            entity.ToTable("InvoiceItem");

            entity.Property(e => e.ItemId).HasColumnName("item_id");
            entity.Property(e => e.Amount)
                .HasDefaultValue(0.00m)
                .HasColumnType("decimal(10, 2)")
                .HasColumnName("amount");
            entity.Property(e => e.Description)
                .HasMaxLength(255)
                .HasColumnName("description");
            entity.Property(e => e.InvoiceId).HasColumnName("invoice_id");
            entity.Property(e => e.Quantity)
                .HasDefaultValue(0.00m)
                .HasColumnType("decimal(8, 2)")
                .HasColumnName("quantity");
            entity.Property(e => e.SessionId).HasColumnName("session_id");
            entity.Property(e => e.UnitPrice)
                .HasDefaultValue(0.00m)
                .HasColumnType("decimal(8, 2)")
                .HasColumnName("unit_price");

            entity.HasOne(d => d.Invoice).WithMany(p => p.InvoiceItems)
                .HasForeignKey(d => d.InvoiceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__InvoiceIt__invoi__32AB8735");

            entity.HasOne(d => d.Session).WithMany(p => p.InvoiceItems)
                .HasForeignKey(d => d.SessionId)
                .HasConstraintName("FK__InvoiceIt__sessi__339FAB6E");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.NotificationId).HasName("PK__Notifica__E059842F71C53EB3");

            entity.ToTable("Notification");

            entity.Property(e => e.NotificationId).HasColumnName("notification_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("created_at");
            entity.Property(e => e.IsRead)
                .HasDefaultValue(false)
                .HasColumnName("is_read");
            entity.Property(e => e.Message).HasColumnName("message");
            entity.Property(e => e.RelatedId).HasColumnName("related_id");
            entity.Property(e => e.Title)
.HasMaxLength(200)
                .HasColumnName("title");
            entity.Property(e => e.Type)
                .HasMaxLength(50)
                .HasColumnName("type");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Notificat__user___245D67DE");
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.PaymentId).HasName("PK__Payment__ED1FC9EA6D5A351A");

            entity.ToTable("Payment");

            entity.HasIndex(e => e.InvoiceNumber, "UQ__Payment__8081A63AE43A58CA").IsUnique();

            entity.Property(e => e.PaymentId).HasColumnName("payment_id");
            entity.Property(e => e.Amount)
                .HasColumnType("decimal(10, 2)")
                .HasColumnName("amount");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("created_at");
            entity.Property(e => e.InvoiceNumber)
                .HasMaxLength(50)
                .HasColumnName("invoice_number");
            entity.Property(e => e.PaymentMethod)
                .HasMaxLength(30)
                .HasColumnName("payment_method");
            entity.Property(e => e.PaymentStatus)
                .HasMaxLength(20)
                .HasDefaultValue("pending")
                .HasColumnName("payment_status");
            entity.Property(e => e.ReservationId).HasColumnName("reservation_id");
            entity.Property(e => e.SessionId).HasColumnName("session_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Reservation).WithMany(p => p.Payments)
                .HasForeignKey(d => d.ReservationId)
                .HasConstraintName("FK__Payment__reserva__02FC7413");

            entity.HasOne(d => d.Session).WithMany(p => p.Payments)
                .HasForeignKey(d => d.SessionId)
                .HasConstraintName("FK__Payment__session__02084FDA");

            entity.HasOne(d => d.User).WithMany(p => p.Payments)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Payment__user_id__01142BA1");
        });

        modelBuilder.Entity<PricingPlan>(entity =>
        {
            entity.HasKey(e => e.PlanId).HasName("PK__PricingP__BE9F8F1D582B80A7");

            entity.ToTable("PricingPlan");

            entity.Property(e => e.PlanId).HasColumnName("plan_id");
            entity.Property(e => e.Benefits).HasColumnName("benefits");
            entity.Property(e => e.BillingCycle)
                .HasMaxLength(20)
                .HasColumnName("billing_cycle");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.DiscountRate)
                            .HasDefaultValue(0.00m)
                            .HasColumnType("decimal(5, 2)")
                            .HasColumnName("discount_rate");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");
            entity.Property(e => e.PlanType)
                .HasMaxLength(20)
                .HasColumnName("plan_type");
            entity.Property(e => e.Price)
                .HasColumnType("decimal(10, 2)")
                .HasColumnName("price");
            entity.Property(e => e.TargetAudience)
                .HasMaxLength(20)
                .HasColumnName("target_audience");
        });

        modelBuilder.Entity<Reservation>(entity =>
        {
            entity.HasKey(e => e.ReservationId).HasName("PK__Reservat__31384C29EDC5BD48");

            entity.ToTable("Reservation");

            entity.Property(e => e.ReservationId).HasColumnName("reservation_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("created_at");
            entity.Property(e => e.DriverId).HasColumnName("driver_id");
            entity.Property(e => e.EndTime).HasColumnName("end_time");
            entity.Property(e => e.PointId).HasColumnName("point_id");
            entity.Property(e => e.StartTime).HasColumnName("start_time");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("booked")
                .HasColumnName("status");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Driver).WithMany(p => p.Reservations)
                .HasForeignKey(d => d.DriverId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Reservati__drive__6C190EBB");

            entity.HasOne(d => d.Point).WithMany(p => p.Reservations)
                .HasForeignKey(d => d.PointId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Reservati__point__6D0D32F4");
        });

        modelBuilder.Entity<SessionLog>(entity =>
        {
            entity.HasKey(e => e.LogId).HasName("PK__SessionL__9E2397E02B25203E");

            entity.ToTable("SessionLog");

            entity.Property(e => e.LogId).HasColumnName("log_id");
            entity.Property(e => e.CurrentPower)
                .HasDefaultValue(0.00m)
                .HasColumnType("decimal(8, 2)")
                .HasColumnName("current_power");
            entity.Property(e => e.LogTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("log_time");
            entity.Property(e => e.SessionId).HasColumnName("session_id");
            entity.Property(e => e.SocPercentage).HasColumnName("soc_percentage");
            entity.Property(e => e.Temperature)
                .HasDefaultValue(0.00m)
                .HasColumnType("decimal(5, 2)")
                .HasColumnName("temperature");
            entity.Property(e => e.Voltage)
                .HasDefaultValue(0.00m)
                .HasColumnType("decimal(8, 2)")
                .HasColumnName("voltage");

            entity.HasOne(d => d.Session).WithMany(p => p.SessionLogs)
                .HasForeignKey(d => d.SessionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__SessionLo__sessi__1EA48E88");
        });

        modelBuilder.Entity<StationStaff>(entity =>
        {
            entity.HasKey(e => e.AssignmentId).HasName("PK__StationS__DA8918140FDA21AB");

            entity.ToTable("StationStaff");

            entity.Property(e => e.AssignmentId).HasColumnName("assignment_id");
            entity.Property(e => e.ShiftEnd).HasColumnName("shift_end");
            entity.Property(e => e.ShiftStart).HasColumnName("shift_start");
            entity.Property(e => e.StaffId).HasColumnName("staff_id");
            entity.Property(e => e.StationId).HasColumnName("station_id");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("active")
                .HasColumnName("status");

            entity.HasOne(d => d.Staff).WithMany(p => p.StationStaffs)
                .HasForeignKey(d => d.StaffId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__StationSt__staff__0C85DE4D");

            entity.HasOne(d => d.Station).WithMany(p => p.StationStaffs)
                .HasForeignKey(d => d.StationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__StationSt__stati__0D7A0286");
        });

        modelBuilder.Entity<Subscription>(entity =>
        {
            entity.HasKey(e => e.SubscriptionId).HasName("PK__Subscrip__863A7EC1FDFD192C");

            entity.ToTable("Subscription");

            entity.Property(e => e.SubscriptionId).HasColumnName("subscription_id");
            entity.Property(e => e.AutoRenew)
                .HasDefaultValue(false)
                .HasColumnName("auto_renew");
            entity.Property(e => e.CorporateId).HasColumnName("corporate_id");
            entity.Property(e => e.EndDate).HasColumnName("end_date");
            entity.Property(e => e.PlanId).HasColumnName("plan_id");
            entity.Property(e => e.StartDate).HasColumnName("start_date");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("active")
                .HasColumnName("status");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Corporate).WithMany(p => p.Subscriptions)
.HasForeignKey(d => d.CorporateId)
                .HasConstraintName("FK__Subscript__corpo__6383C8BA");

            entity.HasOne(d => d.Plan).WithMany(p => p.Subscriptions)
                .HasForeignKey(d => d.PlanId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Subscript__plan___6477ECF3");

            entity.HasOne(d => d.User).WithMany(p => p.Subscriptions)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK__Subscript__user___628FA481");
        });

        modelBuilder.Entity<UsageAnalytic>(entity =>
        {
            entity.HasKey(e => e.AnalyticsId).HasName("PK__UsageAna__D5DC3DE1D0777B60");

            entity.Property(e => e.AnalyticsId).HasColumnName("analytics_id");
            entity.Property(e => e.AnalysisMonth).HasColumnName("analysis_month");
            entity.Property(e => e.FavoriteStationId).HasColumnName("favorite_station_id");
            entity.Property(e => e.PeakUsageHour).HasColumnName("peak_usage_hour");
            entity.Property(e => e.SessionCount)
                .HasDefaultValue(0)
                .HasColumnName("session_count");
            entity.Property(e => e.StationId).HasColumnName("station_id");
            entity.Property(e => e.TotalCost)
                .HasDefaultValue(0.00m)
                .HasColumnType("decimal(10, 2)")
                .HasColumnName("total_cost");
            entity.Property(e => e.TotalEnergyUsed)
                .HasDefaultValue(0.00m)
                .HasColumnType("decimal(10, 2)")
                .HasColumnName("total_energy_used");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.FavoriteStation).WithMany(p => p.UsageAnalyticFavoriteStations)
                .HasForeignKey(d => d.FavoriteStationId)
                .HasConstraintName("FK__UsageAnal__favor__3C34F16F");

            entity.HasOne(d => d.Station).WithMany(p => p.UsageAnalyticStations)
                .HasForeignKey(d => d.StationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__UsageAnal__stati__3B40CD36");

            entity.HasOne(d => d.User).WithMany(p => p.UsageAnalytics)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__UsageAnal__user___3A4CA8FD");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__User__B9BE370F42C3DD18");

            entity.ToTable("User");

            entity.HasIndex(e => e.Email, "UQ__User__AB6E6164DB60277F").IsUnique();

            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.BillingType)
                .HasMaxLength(20)
                .HasColumnName("billing_type");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
.HasColumnName("created_at");
            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .HasColumnName("email");
            entity.Property(e => e.MembershipTier)
                .HasMaxLength(50)
                .HasColumnName("membership_tier");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");
            entity.Property(e => e.Password)
                .HasMaxLength(255)
                .HasColumnName("password");
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .HasColumnName("phone");
            entity.Property(e => e.Role)
                .HasMaxLength(20)
                .HasColumnName("role");
            entity.Property(e => e.WalletBalance)
                .HasDefaultValue(0.00m)
                .HasColumnType("decimal(10, 2)")
                .HasColumnName("wallet_balance");
        });

        modelBuilder.Entity<WalletTransaction>(entity =>
        {
            entity.HasKey(e => e.TransactionId).HasName("PK__WalletTr__85C600AF4953D99E");

            entity.ToTable("WalletTransaction");

            entity.Property(e => e.TransactionId).HasColumnName("transaction_id");
            entity.Property(e => e.Amount)
                .HasColumnType("decimal(10, 2)")
                .HasColumnName("amount");
            entity.Property(e => e.BalanceAfter)
                .HasColumnType("decimal(10, 2)")
                .HasColumnName("balance_after");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("created_at");
            entity.Property(e => e.Description)
                .HasMaxLength(255)
                .HasColumnName("description");
            entity.Property(e => e.ReferenceId).HasColumnName("reference_id");
            entity.Property(e => e.TransactionType)
                .HasMaxLength(20)
                .HasColumnName("transaction_type");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.WalletTransactions)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__WalletTra__user___07C12930");
        });

        modelBuilder.Entity<PasswordResetToken>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_PasswordResetToken");

            entity.ToTable("PasswordResetToken");

            entity.HasIndex(e => e.Token, "UX_PasswordResetToken_Token").IsUnique();

            entity.HasIndex(e => new { e.UserId, e.IsRevoked, e.UsedAt, e.ExpiresAt, e.CreatedAt }, "IX_PasswordResetToken_User_Active");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Token)
                .HasMaxLength(200)
.HasColumnName("token");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("GETDATE()")
                .HasColumnName("created_at");
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
            entity.Property(e => e.UsedAt).HasColumnName("used_at");
            entity.Property(e => e.IsRevoked)
                .HasDefaultValue(false)
                .HasColumnName("is_revoked");

            entity.HasOne(d => d.User).WithMany()
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_PasswordResetToken_User");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}