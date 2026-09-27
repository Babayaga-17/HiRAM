using System;
using System.Collections.Generic;
using ASI.Basecode.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace ASI.Basecode.Data;

public partial class HiramDbContext : DbContext
{
    public HiramDbContext()
    {
    }

    public HiramDbContext(DbContextOptions<HiramDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Account> Accounts { get; set; }

    public virtual DbSet<AuditLog> AuditLogs { get; set; }

    public virtual DbSet<BorrowerProfile> BorrowerProfiles { get; set; }

    public virtual DbSet<Department> Departments { get; set; }

    public virtual DbSet<Equipment> Equipments { get; set; }

    public virtual DbSet<EquipmentCategory> EquipmentCategories { get; set; }

    public virtual DbSet<MaintenanceRecord> MaintenanceRecords { get; set; }

    public virtual DbSet<PasswordResetToken> PasswordResetTokens { get; set; }

    public virtual DbSet<Reservation> Reservations { get; set; }

    public virtual DbSet<ReservationItem> ReservationItems { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<User> Users { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Addr=BORITTT; database=HiramDb; Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(entity =>
        {
            entity.HasKey(e => e.AccountId).HasName("PK__UserAcco__46A222CDF60F1670");

            entity.HasIndex(e => e.RoleId, "IX_UserAccounts_role_id");

            entity.HasIndex(e => e.Email, "UQ_UserAccounts_email").IsUnique();

            entity.HasIndex(e => e.UserId, "UQ_UserAccounts_user_id").IsUnique();

            entity.Property(e => e.AccountId).HasColumnName("account_id");
            entity.Property(e => e.AccountStatus)
                .IsRequired()
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Active")
                .HasColumnName("account_status");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy)
                .IsRequired()
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasDefaultValueSql("(suser_sname())")
                .HasColumnName("created_by");
            entity.Property(e => e.Email)
                .IsRequired()
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("email");
            entity.Property(e => e.FailedLoginAttempts).HasColumnName("failed_login_attempts");
            entity.Property(e => e.LastLoginAt).HasColumnName("last_login_at");
            entity.Property(e => e.LockoutEndAt).HasColumnName("lockout_end_at");
            entity.Property(e => e.PasswordHash)
                .IsRequired()
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("password_hash");
            entity.Property(e => e.RoleId).HasColumnName("role_id");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("updated_by");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Role).WithMany(p => p.Accounts)
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserAccounts_Roles");

            entity.HasOne(d => d.User).WithOne(p => p.Account)
                .HasForeignKey<Account>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserAccounts_Users");
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.AuditLogId).HasName("PK__AuditLog__6031F9F8F61D8187");

            entity.HasIndex(e => e.UserId, "IX_AuditLogs_user_id");

            entity.Property(e => e.AuditLogId).HasColumnName("audit_log_id");
            entity.Property(e => e.Action)
                .IsRequired()
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("action");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy)
                .IsRequired()
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasDefaultValueSql("(suser_sname())")
                .HasColumnName("created_by");
            entity.Property(e => e.Details)
                .IsUnicode(false)
                .HasColumnName("details");
            entity.Property(e => e.EntityId)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("entity_id");
            entity.Property(e => e.EntityName)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("entity_name");
            entity.Property(e => e.IpAddress)
                .HasMaxLength(45)
                .IsUnicode(false)
                .HasColumnName("ip_address");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("updated_by");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.AuditLogs)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_AuditLogs_Users");
        });

        modelBuilder.Entity<BorrowerProfile>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__Borrower__B9BE370FDD3E7201");

            entity.HasIndex(e => e.DepartmentId, "IX_BorrowerProfiles_department_id");

            entity.Property(e => e.UserId)
                .ValueGeneratedNever()
                .HasColumnName("user_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy)
                .IsRequired()
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasDefaultValueSql("(suser_sname())")
                .HasColumnName("created_by");
            entity.Property(e => e.DepartmentId).HasColumnName("department_id");
            entity.Property(e => e.EligibilityStatus)
                .IsRequired()
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Eligible")
                .HasColumnName("eligibility_status");
            entity.Property(e => e.Program)
                .HasMaxLength(150)
                .IsUnicode(false)
                .HasColumnName("program");
            entity.Property(e => e.SuspendedAt).HasColumnName("suspended_at");
            entity.Property(e => e.SuspendedByUserId).HasColumnName("suspended_by_user_id");
            entity.Property(e => e.SuspendedUntil).HasColumnName("suspended_until");
            entity.Property(e => e.SuspensionReason)
                .HasMaxLength(500)
                .IsUnicode(false)
                .HasColumnName("suspension_reason");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("updated_by");
            entity.Property(e => e.YearLevel).HasColumnName("year_level");

            entity.HasOne(d => d.Department).WithMany(p => p.BorrowerProfiles)
                .HasForeignKey(d => d.DepartmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_BorrowerProfiles_Departments");

            entity.HasOne(d => d.SuspendedByUser).WithMany(p => p.BorrowerProfileSuspendedByUsers)
                .HasForeignKey(d => d.SuspendedByUserId)
                .HasConstraintName("FK_BorrowerProfiles_SuspendedBy");

            entity.HasOne(d => d.User).WithOne(p => p.BorrowerProfileUser)
                .HasForeignKey<BorrowerProfile>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_BorrowerProfiles_Users");
        });

        modelBuilder.Entity<Department>(entity =>
        {
            entity.HasKey(e => e.DepartmentId).HasName("PK__Departme__C22324224371E71D");

            entity.HasIndex(e => e.DepartmentCode, "UQ_Departments_department_code").IsUnique();

            entity.HasIndex(e => e.DepartmentName, "UQ_Departments_department_name").IsUnique();

            entity.Property(e => e.DepartmentId).HasColumnName("department_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy)
                .IsRequired()
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasDefaultValueSql("(suser_sname())")
                .HasColumnName("created_by");
            entity.Property(e => e.DepartmentCode)
                .IsRequired()
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("department_code");
            entity.Property(e => e.DepartmentName)
                .IsRequired()
                .HasMaxLength(150)
                .IsUnicode(false)
                .HasColumnName("department_name");
            entity.Property(e => e.Description)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("description");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("updated_by");
        });

        modelBuilder.Entity<Equipment>(entity =>
        {
            entity.HasKey(e => e.EquipmentId).HasName("PK__Equipmen__197068AFB7324C1D");

            entity.HasIndex(e => e.AvailabilityStatus, "IX_EquipmentItems_availability_status");

            entity.HasIndex(e => e.CategoryId, "IX_EquipmentItems_category_id");

            entity.HasIndex(e => e.EquipmentCode, "UQ_EquipmentItems_equipment_code").IsUnique();

            entity.HasIndex(e => e.AssetTag, "UX_EquipmentItems_asset_tag")
                .IsUnique()
                .HasFilter("([asset_tag] IS NOT NULL)");

            entity.HasIndex(e => e.SerialNumber, "UX_EquipmentItems_serial_number")
                .IsUnique()
                .HasFilter("([serial_number] IS NOT NULL)");

            entity.Property(e => e.EquipmentId).HasColumnName("equipment_id");
            entity.Property(e => e.AcquisitionCost)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("acquisition_cost");
            entity.Property(e => e.AcquisitionDate).HasColumnName("acquisition_date");
            entity.Property(e => e.AssetTag)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("asset_tag");
            entity.Property(e => e.AvailabilityStatus)
                .IsRequired()
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Available")
                .HasColumnName("availability_status");
            entity.Property(e => e.Brand)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("brand");
            entity.Property(e => e.CategoryId).HasColumnName("category_id");
            entity.Property(e => e.ConditionStatus)
                .IsRequired()
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Good")
                .HasColumnName("condition_status");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy)
                .IsRequired()
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasDefaultValueSql("(suser_sname())")
                .HasColumnName("created_by");
            entity.Property(e => e.Description)
                .HasMaxLength(500)
                .IsUnicode(false)
                .HasColumnName("description");
            entity.Property(e => e.EquipmentCode)
                .IsRequired()
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("equipment_code");
            entity.Property(e => e.EquipmentName)
                .IsRequired()
                .HasMaxLength(150)
                .IsUnicode(false)
                .HasColumnName("equipment_name");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.Model)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("model");
            entity.Property(e => e.SerialNumber)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("serial_number");
            entity.Property(e => e.StorageLocation)
                .HasMaxLength(150)
                .IsUnicode(false)
                .HasColumnName("storage_location");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("updated_by");

            entity.HasOne(d => d.Category).WithMany(p => p.Equipment)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_EquipmentItems_EquipmentCategories");
        });

        modelBuilder.Entity<EquipmentCategory>(entity =>
        {
            entity.HasKey(e => e.CategoryId).HasName("PK__Equipmen__D54EE9B4E4B21EA0");

            entity.HasIndex(e => e.CategoryCode, "UQ_EquipmentCategories_category_code").IsUnique();

            entity.HasIndex(e => e.CategoryName, "UQ_EquipmentCategories_category_name").IsUnique();

            entity.Property(e => e.CategoryId).HasColumnName("category_id");
            entity.Property(e => e.CategoryCode)
                .IsRequired()
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("category_code");
            entity.Property(e => e.CategoryName)
                .IsRequired()
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("category_name");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy)
                .IsRequired()
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasDefaultValueSql("(suser_sname())")
                .HasColumnName("created_by");
            entity.Property(e => e.Description)
                .HasMaxLength(500)
                .IsUnicode(false)
                .HasColumnName("description");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("updated_by");
        });

        modelBuilder.Entity<MaintenanceRecord>(entity =>
        {
            entity.HasKey(e => e.MaintenanceId).HasName("PK__Maintena__9D754BEA38FE2FDD");

            entity.HasIndex(e => e.EquipmentId, "IX_MaintenanceRecords_equipment_id");

            entity.Property(e => e.MaintenanceId).HasColumnName("maintenance_id");
            entity.Property(e => e.CompletedByUserId).HasColumnName("completed_by_user_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy)
                .IsRequired()
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasDefaultValueSql("(suser_sname())")
                .HasColumnName("created_by");
            entity.Property(e => e.Description)
                .IsRequired()
                .HasMaxLength(500)
                .IsUnicode(false)
                .HasColumnName("description");
            entity.Property(e => e.EndDateTime).HasColumnName("end_date_time");
            entity.Property(e => e.EquipmentId).HasColumnName("equipment_id");
            entity.Property(e => e.MaintenanceType)
                .IsRequired()
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("maintenance_type");
            entity.Property(e => e.Remarks)
                .HasMaxLength(500)
                .IsUnicode(false)
                .HasColumnName("remarks");
            entity.Property(e => e.ReportedByUserId).HasColumnName("reported_by_user_id");
            entity.Property(e => e.StartDateTime).HasColumnName("start_date_time");
            entity.Property(e => e.Status)
                .IsRequired()
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Scheduled")
                .HasColumnName("status");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("updated_by");

            entity.HasOne(d => d.CompletedByUser).WithMany(p => p.MaintenanceRecordCompletedByUsers)
                .HasForeignKey(d => d.CompletedByUserId)
                .HasConstraintName("FK_MaintenanceRecords_completed_by");

            entity.HasOne(d => d.Equipment).WithMany(p => p.MaintenanceRecords)
                .HasForeignKey(d => d.EquipmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MaintenanceRecords_EquipmentItems");

            entity.HasOne(d => d.ReportedByUser).WithMany(p => p.MaintenanceRecordReportedByUsers)
                .HasForeignKey(d => d.ReportedByUserId)
                .HasConstraintName("FK_MaintenanceRecords_reported_by");
        });

        modelBuilder.Entity<PasswordResetToken>(entity =>
        {
            entity.HasKey(e => e.TokenId).HasName("PK__Password__CB3C9E171470D717");

            entity.HasIndex(e => e.TokenHash, "UQ_PasswordResetTokens_token_hash").IsUnique();

            entity.Property(e => e.TokenId).HasColumnName("token_id");
            entity.Property(e => e.AccountId).HasColumnName("account_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy)
                .IsRequired()
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasDefaultValueSql("(suser_sname())")
                .HasColumnName("created_by");
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
            entity.Property(e => e.TokenHash)
                .IsRequired()
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("token_hash");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("updated_by");
            entity.Property(e => e.UsedAt).HasColumnName("used_at");

            entity.HasOne(d => d.Account).WithMany(p => p.PasswordResetTokens)
                .HasForeignKey(d => d.AccountId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PasswordResetTokens_UserAccounts");
        });

        modelBuilder.Entity<Reservation>(entity =>
        {
            entity.HasKey(e => e.ReservationId).HasName("PK__Reservat__31384C291062F136");

            entity.HasIndex(e => e.BorrowerUserId, "IX_Reservations_borrower_user_id");

            entity.HasIndex(e => new { e.StartDateTime, e.ExpectedReturnDateTime }, "IX_Reservations_dates");

            entity.HasIndex(e => e.Status, "IX_Reservations_status");

            entity.HasIndex(e => e.ReservationNumber, "UQ_Reservations_reservation_number").IsUnique();

            entity.Property(e => e.ReservationId).HasColumnName("reservation_id");
            entity.Property(e => e.ApprovedAt).HasColumnName("approved_at");
            entity.Property(e => e.ApprovedByUserId).HasColumnName("approved_by_user_id");
            entity.Property(e => e.BorrowedAt).HasColumnName("borrowed_at");
            entity.Property(e => e.BorrowerUserId).HasColumnName("borrower_user_id");
            entity.Property(e => e.CancellationReason)
                .HasMaxLength(500)
                .IsUnicode(false)
                .HasColumnName("cancellation_reason");
            entity.Property(e => e.CancelledAt).HasColumnName("cancelled_at");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy)
                .IsRequired()
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasDefaultValueSql("(suser_sname())")
                .HasColumnName("created_by");
            entity.Property(e => e.ExpectedReturnDateTime).HasColumnName("expected_return_date_time");
            entity.Property(e => e.Purpose)
                .IsRequired()
                .HasMaxLength(500)
                .IsUnicode(false)
                .HasColumnName("purpose");
            entity.Property(e => e.ReceivedByUserId).HasColumnName("received_by_user_id");
            entity.Property(e => e.RejectedAt).HasColumnName("rejected_at");
            entity.Property(e => e.RejectedByUserId).HasColumnName("rejected_by_user_id");
            entity.Property(e => e.RejectionReason)
                .HasMaxLength(500)
                .IsUnicode(false)
                .HasColumnName("rejection_reason");
            entity.Property(e => e.RequestedAt)
                .HasDefaultValueSql("(sysdatetime())")
                .HasColumnName("requested_at");
            entity.Property(e => e.ReservationNumber)
                .IsRequired()
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("reservation_number");
            entity.Property(e => e.ReturnedAt).HasColumnName("returned_at");
            entity.Property(e => e.RowVersion)
                .IsRequired()
                .IsRowVersion()
                .IsConcurrencyToken()
                .HasColumnName("row_version");
            entity.Property(e => e.StartDateTime).HasColumnName("start_date_time");
            entity.Property(e => e.Status)
                .IsRequired()
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Pending")
                .HasColumnName("status");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("updated_by");

            entity.HasOne(d => d.ApprovedByUser).WithMany(p => p.ReservationApprovedByUsers)
                .HasForeignKey(d => d.ApprovedByUserId)
                .HasConstraintName("FK_Reservations_approved_by");

            entity.HasOne(d => d.BorrowerUser).WithMany(p => p.ReservationBorrowerUsers)
                .HasForeignKey(d => d.BorrowerUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Reservations_borrower");

            entity.HasOne(d => d.ReceivedByUser).WithMany(p => p.ReservationReceivedByUsers)
                .HasForeignKey(d => d.ReceivedByUserId)
                .HasConstraintName("FK_Reservations_received_by");

            entity.HasOne(d => d.RejectedByUser).WithMany(p => p.ReservationRejectedByUsers)
                .HasForeignKey(d => d.RejectedByUserId)
                .HasConstraintName("FK_Reservations_rejected_by");
        });

        modelBuilder.Entity<ReservationItem>(entity =>
        {
            entity.HasKey(e => e.ReservationItemId).HasName("PK__Reservat__83F218BF1A73C97C");

            entity.HasIndex(e => e.EquipmentId, "IX_ReservationItems_equipment_id");

            entity.HasIndex(e => new { e.ReservationId, e.EquipmentId }, "UQ_ReservationItems_reservation_equipment").IsUnique();

            entity.Property(e => e.ReservationItemId).HasColumnName("reservation_item_id");
            entity.Property(e => e.BorrowingRemarks)
                .HasMaxLength(500)
                .IsUnicode(false)
                .HasColumnName("borrowing_remarks");
            entity.Property(e => e.ConditionAtBorrowing)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("condition_at_borrowing");
            entity.Property(e => e.ConditionAtReturn)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("condition_at_return");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy)
                .IsRequired()
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasDefaultValueSql("(suser_sname())")
                .HasColumnName("created_by");
            entity.Property(e => e.DamageNotes)
                .HasMaxLength(500)
                .IsUnicode(false)
                .HasColumnName("damage_notes");
            entity.Property(e => e.EquipmentId).HasColumnName("equipment_id");
            entity.Property(e => e.IsReturned).HasColumnName("is_returned");
            entity.Property(e => e.MissingPartsNotes)
                .HasMaxLength(500)
                .IsUnicode(false)
                .HasColumnName("missing_parts_notes");
            entity.Property(e => e.ReservationId).HasColumnName("reservation_id");
            entity.Property(e => e.ReturnRemarks)
                .HasMaxLength(500)
                .IsUnicode(false)
                .HasColumnName("return_remarks");
            entity.Property(e => e.ReturnedAt).HasColumnName("returned_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("updated_by");

            entity.HasOne(d => d.Equipment).WithMany(p => p.ReservationItems)
                .HasForeignKey(d => d.EquipmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ReservationItems_EquipmentItems");

            entity.HasOne(d => d.Reservation).WithMany(p => p.ReservationItems)
                .HasForeignKey(d => d.ReservationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ReservationItems_Reservations");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.RoleId).HasName("PK__Roles__760965CC4648F8BB");

            entity.HasIndex(e => e.RoleName, "UQ_Roles_role_name").IsUnique();

            entity.Property(e => e.RoleId)
                .ValueGeneratedOnAdd()
                .HasColumnName("role_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy)
                .IsRequired()
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasDefaultValueSql("(suser_sname())")
                .HasColumnName("created_by");
            entity.Property(e => e.Description)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("description");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.RoleName)
                .IsRequired()
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("role_name");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("updated_by");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__Users__B9BE370F75F06690");

            entity.HasIndex(e => e.UserCode, "UQ_Users_user_code").IsUnique();

            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.ContactNumber)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("contact_number");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy)
                .IsRequired()
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasDefaultValueSql("(suser_sname())")
                .HasColumnName("created_by");
            entity.Property(e => e.FirstName)
                .IsRequired()
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("first_name");
            entity.Property(e => e.LastName)
                .IsRequired()
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("last_name");
            entity.Property(e => e.MiddleName)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("middle_name");
            entity.Property(e => e.Suffix)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("suffix");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("updated_by");
            entity.Property(e => e.UserCode)
                .IsRequired()
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("user_code");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
