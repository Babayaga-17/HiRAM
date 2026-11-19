using Microsoft.AspNetCore.Http;
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
using ASI.Basecode.Services.ServiceModels.Borrower;
using ASI.Basecode.Services.ServiceModels.UserAccount;
using ASI.Basecode.Services.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

int passed = 0;
void Check(bool pass, string name) { if (!pass) throw new Exception(name); passed++; Console.WriteLine("PASS " + name); }
async Task Reject(Func<Task> action, string name) { try { await action(); } catch (ValidationException) { Check(true, name); return; } throw new Exception(name); }
// This mode must return before photo uploads or disposable database setup.
if (args.Contains("--academic-only"))
{
    Check(ProgramOptions.ByDepartment.Count == 10 && ProgramOptions.ByDepartment.Values.Sum(p => p.Count) == 20,
        "Program catalog maps ten department codes to twenty programs");
    Check(ProgramOptions.GetPrograms("ccs").Count == 3, "Program lookup accepts department code casing");
    Check(ProgramOptions.GetPrograms("UNKNOWN").Count == 0, "Unmapped department has no programs");
    Check(ProgramOptions.IsProgramAllowed("COE", "BS Computer Engineering"), "Matching program accepted");
    Check(!ProgramOptions.IsProgramAllowed("CCS", "BS Computer Engineering"), "Program from another department rejected");
    Check(!ProgramOptions.IsProgramAllowed("CCS", "BSCS"), "Old test abbreviation rejected");
    Check(!ProgramOptions.IsProgramAllowed("COE", "BSCS"), "Test value cannot move to another department");
    Check(!ProgramOptions.IsProgramAllowed("CCS", "Other"), "Arbitrary program rejected");
    Check(ProgramOptions.IsProgramAllowed("CCS", "BS Computer Science (AI-Focused Elective)"), "Approved full program accepted");
    Check(!ProgramOptions.IsProgramAllowed(null, "BS Computer Engineering")
        && !ProgramOptions.IsProgramAllowed("CCS", null), "Incomplete academic choices rejected");
    Console.WriteLine($"All {passed} database-free academic checks passed.");
    return;
}
if (args.Contains("--roles-only"))
{
    var signIn = new SignInManager();
    Account IdentityAccount(string roleName, string status = AccountStatuses.Active) => new()
    {
        AccountId = 1, UserId = 1, RoleName = roleName, AccountStatus = status,
        Email = "role-check@example.invalid",
        User = new User { UserId = 1, UserCode = "ROLE-CHECK", FirstName = "Role", LastName = "Check" }
    };
    foreach (var roleName in RoleNames.All)
    {
        var identity = signIn.CreateClaimsIdentity(IdentityAccount(roleName));
        Check(identity.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value == roleName,
            $"{roleName} claim comes directly from Account.RoleName");
    }
    foreach (var roleName in new[] { null, "", "Unknown", "admin" })
    {
        var rejected = false;
        try { signIn.CreateClaimsIdentity(IdentityAccount(roleName)); }
        catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "Missing or noncanonical role cannot receive a sign-in identity");
    }
    var pendingRejected = false;
    try { signIn.CreateClaimsIdentity(IdentityAccount(RoleNames.Borrower, AccountStatuses.PendingApproval)); }
    catch (InvalidOperationException) { pendingRejected = true; }
    Check(pendingRejected, "Pending borrower still cannot receive a sign-in identity");

    using var modelDb = new IsolatedTestDb(new DbContextOptionsBuilder<HiramDbContext>()
        .UseSqlServer("Server=127.0.0.1,1;Database=ModelOnly;Integrated Security=True;TrustServerCertificate=True").Options);
    var accountModel = modelDb.Model.FindEntityType(typeof(Account));
    Check(accountModel.FindProperty(nameof(Account.RoleName)) is { IsNullable: false },
        "Scaffolded Account.RoleName is required");
    Check(accountModel.FindProperty("RoleId") == null && accountModel.FindNavigation("Role") == null,
        "Scaffolded account has no legacy role ID or relationship");
    Check(!modelDb.Model.GetEntityTypes().Any(entity => entity.ClrType.Name == "Role"),
        "Legacy Role class is not mapped by the scaffolded context");
    Console.WriteLine($"All {passed} database-free role checks passed.");
    return;
}
var http = new StorageHandler();
var options = Options.Create(new SupabaseStorageOptions { ProjectUrl = "https://example.test", SecretKey = "test-only", Bucket = "equipment-images", ProfileBucket = "profile-images" });
var photoService = new ProfilePhotoService(new HttpClient(http), options, NullLogger<ProfilePhotoService>.Instance);
if (!OperatingSystem.IsWindowsVersionAtLeast(6, 1)) throw new Exception("Image checks require Windows.");
using var image = new Bitmap(700, 350);
using var imageStream = new MemoryStream();
image.Save(imageStream, ImageFormat.Png); imageStream.Position = 0;
IFormFile Photo(Stream stream, string name) => new FormFile(stream, 0, stream.Length, "photo", name);
var path = await photoService.UploadProfileAsync(Photo(imageStream, "personal-name.png"));
Check(path.StartsWith("profiles/") && !path.Contains("personal-name") && http.Paths.Last().Contains("/profile-images/"), "Profile upload uses private profile bucket and random path");
using (var normalized = Image.FromStream(new MemoryStream(http.LastBytes))) Check(normalized.Width == 512 && normalized.Height == 256, "Photo is decoded and resized with aspect ratio preserved");
var before = http.Paths.Count;
await Reject(async () => { await photoService.UploadProfileAsync(Photo(new MemoryStream(new byte[] { 1, 2, 3 }), "renamed.png")); }, "Corrupt image rejected before upload");
await Reject(async () => { await photoService.UploadProfileAsync(Photo(new MemoryStream(new byte[2 * 1024 * 1024 + 1]), "oversized.png")); }, "Oversized image rejected");
await Reject(async () => { await photoService.UploadProfileAsync(Photo(new MemoryStream(new byte[1]), "picture.svg")); }, "Unsupported format rejected");
Check(http.Paths.Count == before, "Invalid images never reach storage");
http.FailUpload = true; imageStream.Position = 0;
try { await photoService.UploadProfileAsync(Photo(imageStream, "picture.png")); throw new Exception("Expected upload failure"); } catch (HttpRequestException) { }
Check(http.Methods.Last() == "DELETE", "Uncertain upload failure triggers cleanup");
http.FailUpload = false;
var equipmentPhotos = new EquipmentPhotoService(new HttpClient(http), options);
await equipmentPhotos.UploadAsync("equipment/test.png", new MemoryStream(new byte[] { 1 }), "image/png");
Check(http.Paths.Last().Contains("/equipment-images/"), "Equipment photo bucket is preserved");

if (args.Contains("--photos-only"))
{
    Console.WriteLine($"All {passed} database-free photo checks passed.");
    return;
}

// New disposable database, never the application's configured database.
var database = "HiRAM_RegistrationChecks_" + Guid.NewGuid().ToString("N");
using var db = new IsolatedTestDb(new DbContextOptionsBuilder<HiramDbContext>().UseSqlServer($"Server=localhost;Database={database};Integrated Security=True;TrustServerCertificate=True").Options);
try
{
    if (db.Database.GetDbConnection().Database != database) throw new InvalidOperationException("Test connection mismatch; stopped before database access.");
    db.Database.EnsureCreated();
    var department = new Department { DepartmentCode = "CCS", DepartmentName = "College of Computer Studies", IsActive = true, CreatedBy = "test" };
    db.Add(department); db.SaveChanges();
    PasswordManager.secretKey = "registration-test-only";
    var users = new UserRepository(new UnitOfWork(db));
    var accounts = new AccountRepository(new UnitOfWork(db));
    var profiles = new BorrowerProfileRepository(new UnitOfWork(db));
    var photos = new FakePhoto(); var email = new FakeEmail();
    BorrowerProfileService Service(IUserRepository repository = null) => new(profiles, repository ?? users, accounts, photos, email, NullLogger<BorrowerProfileService>.Instance, new NotificationService(new NotificationRepository(new UnitOfWork(db))));
    BorrowerRegistrationViewModel Form(string code, string type = "Student") => new() { UserCode = code, BorrowerType = type, FirstName = "Mara", LastName = "Reyes", DepartmentId = department.DepartmentId, Program = "BS Information Technology", YearLevel = 2, Email = code + "@example.test", Password = "TestPass123", ConfirmPassword = "TestPass123" };
    await Service().RegisterAsync(Form("STUDENT-1"), Photo(new MemoryStream(new byte[] { 1 }), "test.png"));
    var saved = db.Users.Include(u => u.Account).Include(u => u.BorrowerProfileUser).Single();
    Check(saved.ProfilePhotoStoragePath == "profiles/test.png" && saved.Account.AccountStatus == "PendingApproval" && saved.BorrowerProfileUser.EligibilityStatus == "Ineligible", "User, pending account, borrower profile and photo path save together");
    Check(saved.Account.RoleName == RoleNames.Borrower && saved.Account.PasswordHash != "TestPass123", "Server assigns Borrower on Account and protects password");
    await Reject(async () => { await Service().RegisterAsync(Form("STUDENT-1"), null); }, "Duplicate school ID rejected");
    var invalid = Form("INVALID"); invalid.DepartmentId = int.MaxValue;
    await Reject(async () => { await Service().RegisterAsync(invalid, null); }, "Unknown department rejected");
    var missingProgram = Form("NO-PROGRAM"); missingProgram.Program = null;
    await Reject(async () => { await Service().RegisterAsync(missingProgram, null); }, "Student program required on server");
    await Service().RegisterAsync(Form("FACULTY-1", "Faculty"), null);
    var faculty = db.Users.Include(u => u.BorrowerProfileUser).Single(u => u.UserCode == "FACULTY-1");
    Check(faculty.ProfilePhotoStoragePath == null && faculty.BorrowerProfileUser.Program == null && faculty.BorrowerProfileUser.YearLevel == null, "Faculty registers without photo or student-only fields");
    try { await Service(new FailingUserRepository(users)).RegisterAsync(Form("FAIL-SAVE"), Photo(new MemoryStream(new byte[1]), "test.png")); throw new Exception("Expected save failure"); } catch (InvalidOperationException) { }
    Check(photos.Deleted == 1 && !db.Users.Any(u => u.UserCode == "FAIL-SAVE"), "Failed registration save deletes uploaded photo");
    email.Fail = true;
    var sent = await Service().RegisterAsync(Form("EMAIL-FAIL"), null);
    Check(!sent && db.Users.Any(u => u.UserCode == "EMAIL-FAIL"), "Email failure does not undo saved registration");

    email.Fail = false;
    var management = new UserAccountService(users, accounts, profiles, new UnitOfWork(db), new NotificationService(new NotificationRepository(new UnitOfWork(db))));
    var login = new AccountService(accounts, users, photos);
    bool Approve(int id, string version, string actor) => management.ApproveRegistrationAsync(id, version, actor).GetAwaiter().GetResult();
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
    db.ChangeTracker.Clear();
    var pendingLogin = login.AuthenticateAccount("STUDENT-1@example.test", "TestPass123");
    Check(pendingLogin.Result == ASI.Basecode.Resources.Constants.Enums.LoginResult.Failed
        && pendingLogin.FailureReason == ASI.Basecode.Resources.Constants.Enums.LoginFailureReason.PendingApproval,
        "Pending credentials are rejected with an awaiting-approval result");
    db.ChangeTracker.Clear();
    RejectApproval(() => management.SetUserAccountStatus(studentId, true, "0", 999, "admin", null), "Generic activation cannot bypass approval");
    var editedPending = new UserAccountFormViewModel(management.GetUserAccountById(studentId));
    editedPending.AccountStatus = "Active";
    RejectApproval(() => management.EditUserAccount(editedPending, 999, "admin", null), "Edit cannot bypass pending approval");
    RejectApproval(() => Approve(studentId, "1", "admin"), "Stale approval rejected");
    Check(email.Attempts == approvalEmailAttempts, "Rejected approvals do not send email");
    var approvalResult = await management.ApproveRegistrationAsync(studentId, "0", "admin");
    Check(approvalResult, "Pending registration approved");
    Check(email.Attempts == approvalEmailAttempts, "Approval service leaves email delivery to the controller");
    Check(db.Notifications.Count(n => n.RecipientUserId == studentId && n.EventType == NotificationEvents.RegistrationApproved) == 1,
        "Approval creates one borrower in-app notification");
    db.ChangeTracker.Clear();
    var approved = users.GetProfileByUserId(studentId);
    Check(approved.Account.AccountStatus == "Active" && approved.BorrowerProfileUser.EligibilityStatus == "Eligible", "Approval persists Active and Eligible together");
    Check(approved.Account.UpdatedBy == "admin" && approved.BorrowerProfileUser.UpdatedBy == "admin", "Both records identify approving actor");
    Check(login.AuthenticateAccount("STUDENT-1@example.test", "TestPass123").Result == ASI.Basecode.Resources.Constants.Enums.LoginResult.Success, "Approved account can authenticate");
    RejectApproval(() => Approve(studentId, approved.Account.UpdatedAt.Value.Ticks.ToString(), "admin"), "Already approved account cannot be approved again");
    Check(!Approve(int.MaxValue, "0", "admin"), "Missing account returns not found");
    Check(email.Attempts == approvalEmailAttempts, "Repeat and missing approvals do not send email");
    Check(db.Notifications.Count(n => n.RecipientUserId == studentId && n.EventType == NotificationEvents.RegistrationApproved) == 1,
        "Repeated approval does not duplicate the in-app notification");

    await Service().RegisterAsync(Form("APPROVAL-ROLLBACK"), null);
    var rollbackId = db.Users.Single(u => u.UserCode == "APPROVAL-ROLLBACK").UserId;
    db.ChangeTracker.Clear();
    var attemptsBeforeFailure = email.Attempts;
    db.Database.ExecuteSqlRaw("CREATE TRIGGER dbo.ApprovalCheckFailure ON dbo.BorrowerProfiles AFTER UPDATE AS BEGIN THROW 51000, 'Simulated profile persistence failure', 1; END;");
    try { Approve(rollbackId, "0", "admin"); throw new Exception("Expected approval failure"); }
    catch (DbUpdateException) { }
    finally { db.ChangeTracker.Clear(); db.Database.ExecuteSqlRaw("DROP TRIGGER dbo.ApprovalCheckFailure;"); }
    var rolledBack = users.GetProfileByUserId(rollbackId);
    Check(rolledBack.Account.AccountStatus == "PendingApproval" && rolledBack.BorrowerProfileUser.EligibilityStatus == "Ineligible", "Database failure rolls back both approval changes");
    Check(!db.Notifications.Any(n => n.RecipientUserId == rollbackId && n.EventType == NotificationEvents.RegistrationApproved),
        "Database failure rolls back the approval notification");
    Check(email.Attempts == attemptsBeforeFailure, "Database failure does not send approval email");
    var blocked = db.BorrowerProfiles.Single(p => p.UserId == rollbackId);
    blocked.EligibilityStatus = "Suspended"; db.SaveChanges(); db.ChangeTracker.Clear();
    RejectApproval(() => Approve(rollbackId, "0", "admin"), "Approval cannot overwrite suspension");

    await Service().RegisterAsync(Form("CONCURRENT-APPROVAL"), null);
    var concurrentId = db.Users.Single(u => u.UserCode == "CONCURRENT-APPROVAL").UserId;
    async Task<bool> ConcurrentApproval() => await Task.Run(() =>
    {
        using var context = new IsolatedTestDb(new DbContextOptionsBuilder<HiramDbContext>()
            .UseSqlServer(db.Database.GetConnectionString()).Options);
        var unit = new UnitOfWork(context);
        var service = new UserAccountService(new UserRepository(unit), new AccountRepository(unit), new BorrowerProfileRepository(unit), unit, new NotificationService(new NotificationRepository(unit)));
        try { return service.ApproveRegistrationAsync(concurrentId, "0", "admin").GetAwaiter().GetResult(); }
        catch (ASI.Basecode.Services.Exceptions.UserAccountValidationException) { return false; }
    });
    var concurrentResults = await Task.WhenAll(ConcurrentApproval(), ConcurrentApproval());
    Check(concurrentResults.Count(result => result) == 1, "Two concurrent approvals produce exactly one success");
    Check(db.Notifications.Count(n => n.RecipientUserId == concurrentId && n.EventType == NotificationEvents.RegistrationApproved) == 1,
        "Concurrent approval commits one in-app notification");

    await Service().RegisterAsync(Form("APPROVAL-EMAIL-FAIL"), null);
    var emailFailId = db.Users.Single(u => u.UserCode == "APPROVAL-EMAIL-FAIL").UserId;
    db.ChangeTracker.Clear();
    email.Fail = true;
    var attemptsBeforeApproval = email.Attempts;
    Check(await management.ApproveRegistrationAsync(emailFailId, "0", "admin"), "Approval does not depend on the email provider");
    Check(email.Attempts == attemptsBeforeApproval, "Approval service does not call the email provider");
    Check(db.Notifications.Any(n => n.RecipientUserId == emailFailId && n.EventType == NotificationEvents.RegistrationApproved),
        "Approval still notifies the borrower when the email provider is unavailable");
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
    public Task<string> UploadProfileAsync(IFormFile photo, CancellationToken token = default) => Task.FromResult("profiles/test.png");
    public Task<string> CreateReadUrlAsync(string path, CancellationToken token = default) => Task.FromResult("https://example.test/photo");
    public Task DeleteAsync(string path, CancellationToken token = default) { Deleted++; return Task.CompletedTask; }
}
class FakeEmail : IEmailManager
{
    public bool Fail;
    public int Attempts;
    public Task SendAsync(string recipient, string subject, string body, CancellationToken token = default)
    {
        Interlocked.Increment(ref Attempts);
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
