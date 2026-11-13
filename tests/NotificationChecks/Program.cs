using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Repositories;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Manager;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Services.ServiceModels.Reservation;
using ASI.Basecode.Services.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

var database = "HiRAM_NotificationChecks_" + Guid.NewGuid().ToString("N");
var connection = $"Server=localhost;Database={database};Integrated Security=True;TrustServerCertificate=True";
TestDb Open() => new(new DbContextOptionsBuilder<HiramDbContext>().UseSqlServer(connection).Options);
using var db = Open();
var passed = 0;
void Check(bool success, string name) { if (!success) throw new Exception(name); passed++; Console.WriteLine("PASS " + name); }
var token = CancellationToken.None;
try
{
    if (db.Database.GetDbConnection().Database != database) throw new Exception("Unexpected database");
    db.Database.EnsureCreated();
    var borrowerRole = new Role { RoleName = "Borrower", IsActive = true, CreatedBy = "test" };
    var adminRole = new Role { RoleName = "Admin", IsActive = true, CreatedBy = "test" };
    var custodianRole = new Role { RoleName = "Custodian", IsActive = true, CreatedBy = "test" };
    User Person(string code, Role role, string status = "Active") => new() { UserCode = code, FirstName = code, LastName = "Test", CreatedBy = "test",
        Account = new Account { Role = role, Email = code + "@example.invalid", PasswordHash = "unused", AccountStatus = status, CreatedBy = "test" } };
    var admin = Person("ADMIN", adminRole); var custodian = Person("STAFF", custodianRole); var disabledAdmin = Person("OFFLINE", adminRole, "Disabled");
    var department = new Department { DepartmentCode = "TEST", DepartmentName = "Test department", IsActive = true, CreatedBy = "test" };
    db.AddRange(admin, custodian, disabledAdmin, department, borrowerRole); db.SaveChanges();
    var unit = new UnitOfWork(db); var repository = new NotificationRepository(unit); var notifications = new NotificationService(repository);
    var users = new UserRepository(unit); var accounts = new AccountRepository(unit); var roles = new RoleRepository(unit);
    var mail = new TestEmail();
    PasswordManager.secretKey = "notification-test-only";
    var registration = new BorrowerProfileService(new BorrowerProfileRepository(unit), users, accounts, roles, new TestPhoto(), mail,
        NullLogger<BorrowerProfileService>.Instance, notifications);
    BorrowerRegistrationViewModel Form(string code) => new() { UserCode = code, BorrowerType = "Faculty", FirstName = "Mara", LastName = "Reyes",
        DepartmentId = department.DepartmentId, Email = code + "@example.invalid", Password = "TestPass123", ConfirmPassword = "TestPass123" };
    await registration.RegisterAsync(Form("BORROWER"), null, null);
    var borrower = db.Users.Include(u => u.Account).Single(u => u.UserCode == "BORROWER");
    Check(db.Notifications.Count(n => n.EventType == NotificationEvents.RegistrationSubmitted) == 1, "New registration notifies only active admins");
    Check(db.Notifications.Any(n => n.RecipientUserId == admin.UserId && n.RelatedUserId == borrower.UserId), "New user's generated ID is linked atomically");
    Check(!db.NotificationEmails.Any() && mail.Attempts == 1, "Registration creates no notification email; direct receipt remains separate");
    var management = new UserAccountService(users, accounts, roles, new BorrowerProfileRepository(unit), unit, notifications);
    Check(await management.ApproveRegistrationAsync(borrower.UserId, "0", "admin"), "Pending registration approved");
    Check(db.Notifications.Count(n => n.EventType == NotificationEvents.RegistrationApproved && n.RecipientUserId == borrower.UserId) == 1, "Approval creates borrower notification");
    Check(!db.NotificationEmails.Any() && mail.Attempts == 1, "Approval creates no queued or direct email");

    var equipment = new Equipment { EquipmentCode = "TEST-EQUIP", EquipmentName = "Test projector", IsActive = true, ConditionStatus = "Good", CreatedBy = "test" };
    db.Add(equipment); db.SaveChanges();
    var equipmentService = new EquipmentService(new EquipmentRepository(unit), null);
    var submissions = new ReservationService(new ReservationRepository(unit), notifications, equipmentService);
    ReservationFormModel Request(int day) => new() { EquipmentIds = new() { equipment.EquipmentId }, Purpose = "Class presentation",
        Pickup = DateTime.Today.AddDays(day).AddHours(10), ExpectedReturn = DateTime.Today.AddDays(day).AddHours(12) };
    var submitted = submissions.Submit(Request(7), borrower.UserId);
    Check(submitted.Error == null, "Reservation submission succeeds with in-app notification");
    var reservation = db.Reservations.Single(r => r.ReservationNumber == submitted.ReservationNumber);
    Check(db.Notifications.Any(n => n.ReservationId == reservation.ReservationId && n.EventType == NotificationEvents.ReservationSubmitted && n.RecipientUserId == custodian.UserId), "Submission notifies custodian");
    var reviews = new ReservationService(new ReservationRepository(unit), notifications, equipmentService);
    Check(reviews.Approve(reservation.ReservationId, custodian.UserId, "staff", null).ok, "Reservation review and notification commit");
    Check(db.Notifications.Any(n => n.ReservationId == reservation.ReservationId && n.EventType == NotificationEvents.ReservationApproved && n.RecipientUserId == borrower.UserId), "Approval notifies requesting borrower");
    Check(!reviews.Approve(reservation.ReservationId, custodian.UserId, "staff", null).ok && db.Notifications.Count(n => n.EventType == NotificationEvents.ReservationApproved) == 1, "Repeated decision does not duplicate notification");
    var rejectedNumber = submissions.Submit(Request(8), borrower.UserId).ReservationNumber;
    var rejected = db.Reservations.Single(r => r.ReservationNumber == rejectedNumber);
    Check(reviews.Reject(rejected.ReservationId, custodian.UserId, "staff", "Equipment needed for an examination").ok, "Rejection succeeds");
    Check(db.Notifications.Any(n => n.EventType == NotificationEvents.ReservationRejected && n.Body.Contains("examination")), "Rejection reason included");
    var cancelledNumber = submissions.Submit(Request(9), borrower.UserId).ReservationNumber;
    var cancelled = db.Reservations.Single(r => r.ReservationNumber == cancelledNumber);
    Check(submissions.Cancel(cancelled.ReservationId, new ReservationCancelFormModel { RowVersion = Convert.ToBase64String(cancelled.RowVersion), Reason = "Schedule changed" }, borrower.UserId) == null, "Cancellation succeeds");
    Check(db.Notifications.Any(n => n.EventType == NotificationEvents.ReservationCancelled && n.RecipientUserId == custodian.UserId), "Cancellation notifies custodian");
    Check(!db.NotificationEmails.Any() && mail.Attempts == 1, "Reservation events create no queued or direct email");

    var menu = await notifications.GetMenuAsync(borrower.UserId, token);
    var own = db.Notifications.AsNoTracking().First(n => n.RecipientUserId == borrower.UserId);
    Check(menu.Items.Count > 0 && menu.UnreadCount == 3, "Menu shows recipient-scoped unread count");
    Check(await notifications.GetDetailsAsync(admin.UserId, own.NotificationId, token) == null, "Another user cannot retrieve notification details");
    Check(!await notifications.OpenAsync(admin.UserId, own.NotificationId, token), "Another user cannot mark a notification read");
    Check(await notifications.OpenAsync(borrower.UserId, own.NotificationId, token), "Recipient can mark notification read");
    var stamp = (await notifications.GetDetailsAsync(borrower.UserId, own.NotificationId, token)).ReadAt;
    await notifications.OpenAsync(borrower.UserId, own.NotificationId, token);
    Check((await notifications.GetDetailsAsync(borrower.UserId, own.NotificationId, token)).ReadAt == stamp, "Repeated opens preserve first read timestamp");
    Check((await notifications.GetMenuAsync(borrower.UserId, token)).UnreadCount == menu.UnreadCount - 1, "Opening message updates unread count");
    for (var i = 0; i < 12; i++) db.Notifications.Add(new Notification { EventKey = Guid.NewGuid(), EventType = NotificationEvents.RegistrationApproved,
        RecipientUserId = borrower.UserId, RelatedUserId = borrower.UserId, Title = "Paging " + i, Body = "Paging test message", CreatedAt = DateTime.UtcNow.AddDays(-1) });
    db.SaveChanges();
    var page = await notifications.GetPageAsync(borrower.UserId, null, "all", 999, token);
    Check(page.Page == page.TotalPages && page.TotalPages == 2 && page.Items.Count == 5, "Server paging clamps out-of-range page");
    Check((await notifications.GetPageAsync(borrower.UserId, "Paging", "unread", 1, token)).TotalCount == 12, "Search and unread filtering execute together");

    db.ChangeTracker.Clear();
    db.Database.ExecuteSqlRaw("CREATE TRIGGER dbo.NotificationCheckFailure ON dbo.Notifications AFTER INSERT AS BEGIN THROW 51001, 'Simulated notification save failure', 1; END;");
    try { await registration.RegisterAsync(Form("ROLLBACK"), null, null); throw new Exception("Expected persistence failure"); }
    catch (DbUpdateException) { }
    finally { db.ChangeTracker.Clear(); db.Database.ExecuteSqlRaw("DROP TRIGGER dbo.NotificationCheckFailure;"); }
    Check(!db.Users.Any(u => u.UserCode == "ROLLBACK"), "Notification failure rolls back registration graph");

    Check(!db.NotificationEmails.Any(), "In-app notification workflows leave the legacy email queue empty");
    Console.WriteLine($"All {passed} notification checks passed.");
}
finally
{
    if (db.Database.GetDbConnection().Database != database || !database.StartsWith("HiRAM_NotificationChecks_")) throw new Exception("Cleanup target mismatch");
    db.Database.EnsureDeleted();
}

sealed class TestDb(DbContextOptions<HiramDbContext> options) : HiramDbContext(options)
{ protected override void OnConfiguring(DbContextOptionsBuilder builder) { } }
sealed class TestEmail : IEmailManager
{
    public int Attempts;
    public Task SendAsync(string recipient, string subject, string body, CancellationToken token = default)
    { Attempts++; return Task.CompletedTask; }
}
sealed class TestPhoto : IProfilePhotoService
{
    public Task<string> UploadProfileAsync(Stream stream, string file, CancellationToken token = default) => Task.FromResult("profiles/test.png");
    public Task<string> CreateReadUrlAsync(string path, CancellationToken token = default) => Task.FromResult("https://example.invalid/photo");
    public Task DeleteAsync(string path, CancellationToken token = default) => Task.CompletedTask;
}
