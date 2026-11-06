using System.ComponentModel.DataAnnotations;
using ASI.Basecode.WebApp.Authentication;
using ASI.Basecode.Resources.Constants;
using System.Drawing;
using System.Drawing.Imaging;
using System.Net;
using ASI.Basecode.Data;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Repositories;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Manager;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Services.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

int passed = 0;
void Check(bool pass, string name) { if (!pass) throw new Exception(name); passed++; Console.WriteLine("PASS " + name); }
async Task Reject(Func<Task> action, string name) { try { await action(); } catch (ValidationException) { Check(true, name); return; } throw new Exception(name); }
var http = new StorageHandler();
var options = Options.Create(new SupabaseStorageOptions { ProjectUrl = "https://example.test", SecretKey = "test-only", Bucket = "equipment-images", ProfileBucket = "profile-images" });
var photoService = new ProfilePhotoService(new HttpClient(http), options, NullLogger<ProfilePhotoService>.Instance);
if (!OperatingSystem.IsWindowsVersionAtLeast(6, 1)) throw new Exception("Image checks require Windows.");
using var image = new Bitmap(700, 350);
using var imageStream = new MemoryStream();
image.Save(imageStream, ImageFormat.Png); imageStream.Position = 0;
var path = await photoService.UploadProfileAsync(imageStream, "personal-name.png");
Check(path.StartsWith("profiles/") && !path.Contains("personal-name") && http.Paths.Last().Contains("/profile-images/"), "Profile upload uses private profile bucket and random path");
using (var normalized = Image.FromStream(new MemoryStream(http.LastBytes))) Check(normalized.Width == 512 && normalized.Height == 256, "Photo is decoded and resized with aspect ratio preserved");
var before = http.Paths.Count;
await Reject(async () => { await photoService.UploadProfileAsync(new MemoryStream(new byte[] { 1, 2, 3 }), "renamed.png"); }, "Corrupt image rejected before upload");
await Reject(async () => { await photoService.UploadProfileAsync(new MemoryStream(new byte[2 * 1024 * 1024 + 1]), "oversized.png"); }, "Oversized image rejected");
await Reject(async () => { await photoService.UploadProfileAsync(new MemoryStream(new byte[1]), "picture.svg"); }, "Unsupported format rejected");
Check(http.Paths.Count == before, "Invalid images never reach storage");
http.FailUpload = true; imageStream.Position = 0;
try { await photoService.UploadProfileAsync(imageStream, "picture.png"); throw new Exception("Expected upload failure"); } catch (HttpRequestException) { }
Check(http.Methods.Last() == "DELETE", "Uncertain upload failure triggers cleanup");
http.FailUpload = false;
var equipmentPhotos = new EquipmentPhotoService(new HttpClient(http), options);
await equipmentPhotos.UploadAsync("equipment/test.png", new MemoryStream(new byte[] { 1 }), "image/png");
Check(http.Paths.Last().Contains("/equipment-images/"), "Equipment photo bucket is preserved");

// New disposable database, never the application's configured database.
var database = "HiRAM_RegistrationChecks_" + Guid.NewGuid().ToString("N");
using var db = new IsolatedTestDb(new DbContextOptionsBuilder<HiramDbContext>().UseSqlServer($"Server=localhost;Database={database};Integrated Security=True;TrustServerCertificate=True").Options);
try
{
    if (db.Database.GetDbConnection().Database != database) throw new InvalidOperationException("Test connection mismatch; stopped before database access.");
    db.Database.EnsureCreated();
    var department = new Department { DepartmentCode = "TEST", DepartmentName = "Test department", IsActive = true, CreatedBy = "test" };
    db.Add(department); db.Add(new Role { RoleName = "Borrower", IsActive = true, CreatedBy = "test" }); db.SaveChanges();
    PasswordManager.secretKey = "registration-test-only";
    var users = new UserRepository(new UnitOfWork(db));
    var accounts = new AccountRepository(new UnitOfWork(db));
    var roles = new RoleRepository(new UnitOfWork(db));
    var profiles = new BorrowerProfileRepository(new UnitOfWork(db));
    var photos = new FakePhoto(); var email = new FakeEmail();
    BorrowerProfileService Service(IUserRepository repository = null) => new(profiles, repository ?? users, accounts, roles, photos, email, NullLogger<BorrowerProfileService>.Instance, new NotificationService(new NotificationRepository(new UnitOfWork(db))));
    BorrowerRegistrationViewModel Form(string code, string type = "Student") => new() { UserCode = code, BorrowerType = type, FirstName = "Mara", LastName = "Reyes", DepartmentId = department.DepartmentId, Program = "Computer Science", YearLevel = 2, Email = code + "@example.test", Password = "TestPass123", ConfirmPassword = "TestPass123" };
    await Service().RegisterAsync(Form("STUDENT-1"), new MemoryStream(new byte[] { 1 }), "test.png");
    var saved = db.Users.Include(u => u.Account).Include(u => u.BorrowerProfileUser).Single();
    Check(saved.ProfilePhotoStoragePath == "profiles/test.png" && saved.Account.AccountStatus == "PendingApproval" && saved.BorrowerProfileUser.EligibilityStatus == "Ineligible", "User, pending account, borrower profile and photo path save together");
    Check(saved.Account.RoleId == roles.GetRoles().Single().RoleId && saved.Account.PasswordHash != "TestPass123", "Server assigns Borrower and protects password");
    await Reject(async () => { await Service().RegisterAsync(Form("STUDENT-1"), null, null); }, "Duplicate school ID rejected");
    var invalid = Form("INVALID"); invalid.DepartmentId = int.MaxValue;
    await Reject(async () => { await Service().RegisterAsync(invalid, null, null); }, "Unknown department rejected");
    var missingProgram = Form("NO-PROGRAM"); missingProgram.Program = null;
    await Reject(async () => { await Service().RegisterAsync(missingProgram, null, null); }, "Student program required on server");
    await Service().RegisterAsync(Form("FACULTY-1", "Faculty"), null, null);
    var faculty = db.Users.Include(u => u.BorrowerProfileUser).Single(u => u.UserCode == "FACULTY-1");
    Check(faculty.ProfilePhotoStoragePath == null && faculty.BorrowerProfileUser.Program == null && faculty.BorrowerProfileUser.YearLevel == null, "Faculty registers without photo or student-only fields");
    try { await Service(new FailingUserRepository(users)).RegisterAsync(Form("FAIL-SAVE"), new MemoryStream(new byte[1]), "test.png"); throw new Exception("Expected save failure"); } catch (InvalidOperationException) { }
    Check(photos.Deleted == 1 && !db.Users.Any(u => u.UserCode == "FAIL-SAVE"), "Failed registration save deletes uploaded photo");
    email.Fail = true;
    var sent = await Service().RegisterAsync(Form("EMAIL-FAIL"), null, null);
    Check(!sent && db.Users.Any(u => u.UserCode == "EMAIL-FAIL"), "Email failure does not undo saved registration");

    email.Fail = false;
    var management = new UserAccountService(users, accounts, roles, new UnitOfWork(db), new NotificationService(new NotificationRepository(new UnitOfWork(db))));
    var login = new AccountService(accounts, users, photos);
    bool Approve(int id, string version, string actor) => management.ApproveRegistrationAsync(id, version, actor).GetAwaiter().GetResult().Found;
    void RejectApproval(Action action, string name)
    {
        try { action(); }
        catch (ASI.Basecode.Services.Exceptions.UserAccountValidationException) { db.ChangeTracker.Clear(); Check(true, name); return; }
        throw new Exception(name);
    }
    var studentId = saved.UserId;
    var identityFactory = new SignInManager();
    try { identityFactory.CreateClaimsIdentity(accounts.GetAccountByUserId(studentId)); throw new Exception("Pending sign-in should be rejected"); }
    catch (InvalidOperationException) { Check(true, "Pending account cannot receive a sign-in identity"); }
    var approvalEmailAttempts = email.Attempts;
    var committedBeforeEmail = false;
    email.BeforeSend = () =>
    {
        using var verification = new IsolatedTestDb(new DbContextOptionsBuilder<HiramDbContext>()
            .UseSqlServer(db.Database.GetConnectionString()).Options);
        committedBeforeEmail = verification.Accounts.Any(a => a.UserId == studentId && a.AccountStatus == "Active")
            && verification.BorrowerProfiles.Any(p => p.UserId == studentId && p.EligibilityStatus == "Eligible");
    };
    db.ChangeTracker.Clear();
    Account loginAccount = null;
    Check(login.AuthenticateAccount("STUDENT-1@example.test", "TestPass123", ref loginAccount) == ASI.Basecode.Resources.Constants.Enums.LoginResult.PendingApproval, "Pending credentials are rejected with an awaiting-approval result");
    db.ChangeTracker.Clear();
    RejectApproval(() => management.SetUserAccountStatus(studentId, true, "0", 999, "admin", null), "Generic activation cannot bypass approval");
    var editedPending = new UserAccountFormViewModel(management.GetUserAccountById(studentId));
    editedPending.AccountStatus = "Active";
    RejectApproval(() => management.EditUserAccount(editedPending, 999, "admin", null), "Edit cannot bypass pending approval");
    RejectApproval(() => Approve(studentId, "1", "admin"), "Stale approval rejected");
    Check(email.Attempts == approvalEmailAttempts, "Rejected approvals do not send email");
    var approvalResult = await management.ApproveRegistrationAsync(studentId, "0", "admin");
    Check(approvalResult.Found && approvalResult.EmailQueued, "Pending registration approved and email queued");
    Check(email.Attempts == approvalEmailAttempts, "Approval does not send SMTP within its request");
    var dispatcher = new NotificationEmailDispatcher(new NotificationRepository(new UnitOfWork(db)), email, NullLogger<NotificationEmailDispatcher>.Instance);
    await dispatcher.DispatchNextAsync(CancellationToken.None);
    Check(committedBeforeEmail && email.Attempts == approvalEmailAttempts + 1, "Approval email runs after both records commit");
    Check(email.LastRecipient == "STUDENT-1@example.test" && email.LastSubject == "Your HiRAM registration is approved", "Approval notification targets registered email");
    email.BeforeSend = null;
    db.ChangeTracker.Clear();
    var approved = users.GetProfileByUserId(studentId);
    Check(approved.Account.AccountStatus == "Active" && approved.BorrowerProfileUser.EligibilityStatus == "Eligible", "Approval persists Active and Eligible together");
    Check(approved.Account.UpdatedBy == "admin" && approved.BorrowerProfileUser.UpdatedBy == "admin", "Both records identify approving actor");
    Check(login.AuthenticateAccount("STUDENT-1@example.test", "TestPass123", ref loginAccount) == ASI.Basecode.Resources.Constants.Enums.LoginResult.Success, "Approved account can authenticate");
    RejectApproval(() => Approve(studentId, approved.Account.UpdatedAt.Value.Ticks.ToString(), "admin"), "Already approved account cannot be approved again");
    Check(!Approve(int.MaxValue, "0", "admin"), "Missing account returns not found");
    Check(email.Attempts == approvalEmailAttempts + 1, "Repeat and missing approvals do not send email");

    await Service().RegisterAsync(Form("APPROVAL-ROLLBACK"), null, null);
    var rollbackId = db.Users.Single(u => u.UserCode == "APPROVAL-ROLLBACK").UserId;
    db.ChangeTracker.Clear();
    var attemptsBeforeFailure = email.Attempts;
    db.Database.ExecuteSqlRaw("CREATE TRIGGER dbo.ApprovalCheckFailure ON dbo.BorrowerProfiles AFTER UPDATE AS BEGIN THROW 51000, 'Simulated profile persistence failure', 1; END;");
    try { Approve(rollbackId, "0", "admin"); throw new Exception("Expected approval failure"); }
    catch (DbUpdateException) { }
    finally { db.ChangeTracker.Clear(); db.Database.ExecuteSqlRaw("DROP TRIGGER dbo.ApprovalCheckFailure;"); }
    var rolledBack = users.GetProfileByUserId(rollbackId);
    Check(rolledBack.Account.AccountStatus == "PendingApproval" && rolledBack.BorrowerProfileUser.EligibilityStatus == "Ineligible", "Database failure rolls back both approval changes");
    Check(email.Attempts == attemptsBeforeFailure, "Database failure does not send approval email");
    var blocked = db.BorrowerProfiles.Single(p => p.UserId == rollbackId);
    blocked.EligibilityStatus = "Suspended"; db.SaveChanges(); db.ChangeTracker.Clear();
    RejectApproval(() => Approve(rollbackId, "0", "admin"), "Approval cannot overwrite suspension");

    await Service().RegisterAsync(Form("CONCURRENT-APPROVAL"), null, null);
    var concurrentId = db.Users.Single(u => u.UserCode == "CONCURRENT-APPROVAL").UserId;
    async Task<bool> ConcurrentApproval() => await Task.Run(() =>
    {
        using var context = new IsolatedTestDb(new DbContextOptionsBuilder<HiramDbContext>()
            .UseSqlServer(db.Database.GetConnectionString()).Options);
        var unit = new UnitOfWork(context);
        var service = new UserAccountService(new UserRepository(unit), new AccountRepository(unit), new RoleRepository(unit), unit, new NotificationService(new NotificationRepository(unit)));
        try { return service.ApproveRegistrationAsync(concurrentId, "0", "admin").GetAwaiter().GetResult().Found; }
        catch (ASI.Basecode.Services.Exceptions.UserAccountValidationException) { return false; }
    });
    var concurrentResults = await Task.WhenAll(ConcurrentApproval(), ConcurrentApproval());
    Check(concurrentResults.Count(result => result) == 1, "Two concurrent approvals produce exactly one success");

    await Service().RegisterAsync(Form("APPROVAL-EMAIL-FAIL"), null, null);
    var emailFailId = db.Users.Single(u => u.UserCode == "APPROVAL-EMAIL-FAIL").UserId;
    db.ChangeTracker.Clear();
    email.Fail = true;
    var emailFailure = await management.ApproveRegistrationAsync(emailFailId, "0", "admin");
    Check(emailFailure.Found && emailFailure.EmailQueued, "Approval queues email independently of provider failures");
    while (await dispatcher.DispatchNextAsync(CancellationToken.None)) { }
    Check(db.NotificationEmails.Any(e => e.Notification.RecipientUserId == emailFailId && e.DeliveryStatus == "Pending" && e.AttemptCount == 1), "Email failure schedules a retry");
    db.ChangeTracker.Clear();
    var emailFailedUser = users.GetProfileByUserId(emailFailId);
    Check(emailFailedUser.Account.AccountStatus == "Active" && emailFailedUser.BorrowerProfileUser.EligibilityStatus == "Eligible", "Email failure preserves both approval changes");
}
finally {
    if (db.Database.GetDbConnection().Database != database || !database.StartsWith("HiRAM_RegistrationChecks_"))
        throw new InvalidOperationException("Cleanup refused: test database mismatch.");
    db.Database.EnsureDeleted();
}
Console.WriteLine($"All {passed} registration/photo checks passed.");

class StorageHandler : HttpMessageHandler
{
    public List<string> Paths = new(), Methods = new();
    public byte[] LastBytes;
    public bool FailUpload;
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        Paths.Add(request.RequestUri.AbsolutePath); Methods.Add(request.Method.Method);
        LastBytes = request.Content == null ? Array.Empty<byte>() : await request.Content.ReadAsByteArrayAsync(token);
        return new HttpResponseMessage(FailUpload && request.Method == HttpMethod.Post ? HttpStatusCode.InternalServerError : HttpStatusCode.OK) { Content = new StringContent("{}") };
    }
}
class FakePhoto : IProfilePhotoService
{
    public int Deleted;
    public Task<string> UploadProfileAsync(Stream content, string name, CancellationToken token = default) => Task.FromResult("profiles/test.png");
    public Task<string> CreateReadUrlAsync(string path, CancellationToken token = default) => Task.FromResult("https://example.test/photo");
    public Task DeleteAsync(string path, CancellationToken token = default) { Deleted++; return Task.CompletedTask; }
}
class FakeEmail : IEmailManager
{
    public bool Fail;
    public int Attempts;
    public string LastRecipient, LastSubject;
    public Action BeforeSend;
    public Task SendAsync(string recipient, string subject, string body, CancellationToken token = default)
    {
        Interlocked.Increment(ref Attempts);
        LastRecipient = recipient; LastSubject = subject;
        BeforeSend?.Invoke();
        return Fail ? Task.FromException(new Exception("Simulated email failure")) : Task.CompletedTask;
    }
}
class FailingUserRepository(IUserRepository inner) : IUserRepository
{
    public IQueryable<User> GetUsers() => inner.GetUsers();
    public bool UserExists(int id) => inner.UserExists(id);
    public User GetProfileByUserId(int id) => inner.GetProfileByUserId(id);
    public bool UserCodeExists(string code, int? exclude = null) => inner.UserCodeExists(code, exclude);
    public void AddUser(User user) => throw new InvalidOperationException("Simulated save failure");
}

sealed class IsolatedTestDb(DbContextOptions<HiramDbContext> options) : HiramDbContext(options)
{
    protected override void OnConfiguring(DbContextOptionsBuilder builder) { }
}
