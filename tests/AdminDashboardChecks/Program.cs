using System.Reflection;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Services;
using ASI.Basecode.Services.ServiceModels;

static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
static T Stub<T>(Func<string, object[], object> read) where T : class
{
    var proxy = DispatchProxy.Create<T, ReadProxy>();
    ((ReadProxy)(object)proxy).Read = read;
    return proxy;
}
var calls = new List<string>();
var counts = new Dictionary<string, int> { [AccountStatuses.Active] = 10, [AccountStatuses.PendingApproval] = 5, [AccountStatuses.Inactive] = 2 };
var preview = Enumerable.Range(1, 4).Select(id => new UserAccountListItem { UserId = id, FirstName = "Test", LastName = $"{id}", AccountStatus = AccountStatuses.PendingApproval }).ToList();
string failAt = null;
TaskCompletionSource<Dictionary<string, int>> gate = null;
var accounts = Stub<IUserAccountService>((name, args) => {
    calls.Add(name);
    if (name == "CountAccountsByStatusAsync")
        return failAt == name ? Task.FromException<Dictionary<string, int>>(new InvalidOperationException("Count failure")) : gate?.Task ?? Task.FromResult(counts);
    Check(name == "GetPendingRegistrationPreviewAsync", "Admin retrieved a full account list or called a mutation");
    return failAt == name ? Task.FromException<List<UserAccountListItem>>(new InvalidOperationException("Preview failure")) : Task.FromResult(preview);
});
var equipment = Stub<IEquipmentService>((name, args) => {
    calls.Add(name);
    Check(name == "CountEquipmentAsync", "Admin loaded an equipment page or called a mutation");
    return failAt == name ? Task.FromException<int>(new InvalidOperationException("Equipment failure")) : Task.FromResult(8);
});
// Only the lightweight account/equipment services are supplied for the Admin branch.
var service = new DashboardService(null, null, accounts, equipment);
var model = await service.GetAdminOverviewAsync();
Check(model.TotalUsers == 17 && model.PendingRegistrationCount == 5 && model.TotalEquipment == 8, "Admin totals mismatch");
Check(model.RegistrationPreview.Select(a => a.UserId).SequenceEqual(new[]{1,2,3,4}), "Preview order was changed");
Check(calls.SequenceEqual(new[]{"CountAccountsByStatusAsync","GetPendingRegistrationPreviewAsync","CountEquipmentAsync"}), "Unexpected, repeated or out-of-order reads");
foreach (var operation in new[]{"CountAccountsByStatusAsync","GetPendingRegistrationPreviewAsync","CountEquipmentAsync"})
{
    calls.Clear(); failAt = operation;
    try { await service.GetAdminOverviewAsync(); throw new Exception("Service swallowed a retrieval failure"); }
    catch (InvalidOperationException) { }
    Check(calls.Last() == operation, "Service continued querying after failure");
}
failAt = null; calls.Clear(); counts = new(); preview = new();
model = await service.GetAdminOverviewAsync();
Check(model.TotalUsers == 0 && model.PendingRegistrationCount == 0 && model.RegistrationPreview.Count == 0, "Real empty results were not zero/empty");
counts = new() { [AccountStatuses.Active] = 2 };
model = await service.GetAdminOverviewAsync();
Check(model.TotalUsers == 2 && model.PendingRegistrationCount == 0, "Absent pending status should count as zero");
calls.Clear(); gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
var pending = service.GetAdminOverviewAsync();
Check(!pending.IsCompleted && calls.SequenceEqual(new[]{"CountAccountsByStatusAsync"}), "Queries overlapped before count retrieval completed");
gate.SetResult(counts); await pending;
Check(new AdminDashboardViewModel().TotalUsers == null && new AdminDashboardViewModel().RegistrationPreview == null, "Unavailable defaults must remain null");
Console.WriteLine("PASS: lightweight Admin counts and preview, totals, ordering, sequential reads, failure propagation and zero/unavailable semantics (fake dependencies; no database).");
public class ReadProxy : DispatchProxy
{
    public Func<string, object[], object> Read { get; set; }
    protected override object Invoke(MethodInfo method, object[] args) => Read(method.Name, args);
}
