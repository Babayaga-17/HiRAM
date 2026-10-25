using System.ComponentModel.DataAnnotations;
using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Repositories;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Services.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

// Uses a new disposable SQL Server database, never the application's HiramDb.
var database = "HiRAM_ReservationChecks_" + Guid.NewGuid().ToString("N");
var connection = $"Server=localhost;Database={database};Integrated Security=True;TrustServerCertificate=True";
using var db = Open();
var checks = 0;
try
{
    db.Database.EnsureCreated();
    var user = new User { UserCode = "TEST", FirstName = "Test", LastName = "Borrower", CreatedBy = "test" };
    user.Account = new Account { Email = "reservation-test@example.invalid", PasswordHash = "unused",
        AccountStatus = "Active", CreatedBy = "test",
        Role = new Role { RoleName = "Borrower", IsActive = true, CreatedBy = "test" } };
    user.BorrowerProfileUser = new BorrowerProfile { EligibilityStatus = "Eligible", CreatedBy = "test",
        Department = new Department { DepartmentCode = "TEST", DepartmentName = "Test", IsActive = true, CreatedBy = "test" } };
    db.Users.Add(user);
    var first = NewEquipment("A");
    var second = NewEquipment("B");
    db.AddRange(first, second);
    db.SaveChanges();
    var userId = user.UserId;
    var start = DateTime.Today.AddDays(7).AddHours(10);

    ReservationFormModel Form(DateTime? pickup = null, DateTime? end = null, params int[] ids) => new()
    {
        EquipmentIds = ids.Length > 0 ? ids.ToList() : new() { first.EquipmentId },
        Pickup = pickup ?? start, ExpectedReturn = end ?? start.AddHours(2), Purpose = "Class presentation"
    };
    (string ReservationNumber, string Error) Submit(ReservationFormModel form) => Service(db).Submit(form, userId);
    void Check(bool pass, string message)
    {
        if (!pass) throw new Exception("FAILED: " + message);
        checks++;
        Console.WriteLine("PASS: " + message);
    }

    Check(Submit(Form()).Error == null, "Valid request saves");
    var saved = db.Reservations.Include(r => r.ReservationItems).Single();
    Check(saved.Status == "Pending" && saved.BorrowerUserId == userId && saved.ReservationItems.Count == 1,
        "Reservation and item are persisted with the authenticated borrower");
    Check(Submit(Form()).Error != null, "Pending duplicate is rejected");
    Check(Submit(Form(start.AddHours(-1), start.AddHours(1))).Error != null, "Overlap at start is rejected");
    Check(Submit(Form(start.AddHours(1), start.AddHours(3))).Error != null, "Overlap at end is rejected");
    Check(Submit(Form(start.AddMinutes(15), start.AddMinutes(45))).Error != null, "Contained interval is rejected");
    Check(Submit(Form(start.AddHours(-1), start.AddHours(3))).Error != null, "Enclosing interval is rejected");
    Check(Submit(Form(start.AddHours(2), start.AddHours(3))).Error == null, "Back-to-back reservation is accepted");
    Check(Submit(Form(start, start)).Error != null, "Zero-length period is rejected");
    Check(Submit(Form(DateTime.Today.AddDays(-1), start)).Error != null, "Past pickup is rejected");
    var emptyPurpose = Form(); emptyPurpose.Purpose = "   ";
    Check(Submit(emptyPurpose).Error != null, "Blank purpose is rejected");
    var missing = Form(); missing.EquipmentIds.Clear();
    Check(Submit(missing).Error != null, "Empty selection is rejected");
    Check(Submit(Form(ids: new[] { int.MaxValue })).Error != null, "Unknown equipment is rejected");

    var count = db.Reservations.Count();
    Check(Submit(Form(ids: new[] { second.EquipmentId, first.EquipmentId })).Error != null
        && db.Reservations.Count() == count, "One conflicting item prevents the whole request from saving");
    second.IsActive = false; db.SaveChanges();
    Check(Submit(Form(ids: new[] { second.EquipmentId })).Error != null, "Inactive equipment is rejected");
    second.IsActive = true; db.SaveChanges();

    foreach (var status in new[] { "Approved", "Borrowed", "Overdue" })
    {
        saved.Status = status; db.SaveChanges();
        Check(Submit(Form()).Error != null, status + " overlap is rejected");
    }
    foreach (var status in new[] { "Cancelled", "Rejected", "Returned" })
    {
        saved.Status = status; db.SaveChanges();
        var accepted = Submit(Form());
        Check(accepted.Error == null, status + " reservation does not block the slot");
        var acceptedReservation = db.Reservations.Single(r => r.ReservationNumber == accepted.ReservationNumber);
        db.ReservationItems.RemoveRange(db.ReservationItems.Where(item => item.ReservationId == acceptedReservation.ReservationId));
        db.SaveChanges();
        db.Reservations.Remove(acceptedReservation);
        db.SaveChanges();
    }

    db.MaintenanceRecords.Add(new MaintenanceRecord { EquipmentId = second.EquipmentId,
        MaintenanceType = "Repair", Description = "Test", Status = "Scheduled", StartDateTime = start,
        EndDateTime = start.AddHours(2), CreatedBy = "test" });
    db.SaveChanges();
    Check(Submit(Form(ids: new[] { second.EquipmentId })).Error != null, "Scheduled maintenance blocks reservation");
    user.BorrowerProfileUser.EligibilityStatus = "Suspended"; db.SaveChanges();
    Check(Submit(Form(start.AddDays(1), start.AddDays(1).AddHours(1))).Error != null, "Suspended borrower is rejected");
    user.BorrowerProfileUser.EligibilityStatus = "Eligible"; db.SaveChanges();

    using var gate = new Barrier(2);
    var concurrent = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => Task.Run(() =>
    {
        using var competingDb = Open();
        gate.SignalAndWait();
        try { return Service(competingDb).Submit(Form(start.AddDays(2), start.AddDays(2).AddHours(1)), userId).Error == null; }
        catch (Exception error) when (error.GetBaseException() is SqlException sql && sql.Number == 1205)
        { return false; } // SQL Server may abort one serializable transaction to break a deadlock.
    })));
    Check(concurrent.Count(success => success) == 1
        && db.Reservations.Count(r => r.StartDateTime == start.AddDays(2)) == 1,
        "Concurrent conflicting submissions save exactly one reservation");
    Console.WriteLine($"All {checks} checks passed.");
}
finally
{
    // Guard cleanup so this can never target HiramDb or another named database.
    if (!database.StartsWith("HiRAM_ReservationChecks_") || database.Length != 56)
        throw new InvalidOperationException("Unexpected test database name; cleanup stopped.");
    db.Database.EnsureDeleted();
}

TestDb Open() => new(new DbContextOptionsBuilder<HiramDbContext>().UseSqlServer(connection).Options);
Equipment NewEquipment(string code) => new() { EquipmentCode = code, EquipmentName = "Test " + code,
    ConditionStatus = "Good", IsActive = true, CreatedBy = "test" };
EquipmentReservationService Service(HiramDbContext context) => new(context,
    new EquipmentService(new EquipmentRepository(new UnitOfWork(context)), null));

sealed class TestDb(DbContextOptions<HiramDbContext> options) : HiramDbContext(options)
{
    protected override void OnConfiguring(DbContextOptionsBuilder builder) { }
}
