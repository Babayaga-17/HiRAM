using System.Reflection;
using System.Security.Claims;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Services.ServiceModels.Dashboard;
using ASI.Basecode.Services.Services;
using ASI.Basecode.WebApp.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

static T Stub<T>(Func<string, object[], object> read) where T : class { var proxy=DispatchProxy.Create<T, ReadProxy>(); ((ReadProxy)(object)proxy).Read=read; return proxy; }
static void Check(bool condition, string message) { if(!condition) throw new Exception(message); }
var now=DateTime.Now;
var rows=new List<Reservation> {
 new(){ReservationId=1,BorrowerUserId=123,ReservationNumber="CHECK-1",Status="Borrowed",StartDateTime=now.AddDays(-2),ExpectedReturnDateTime=now.AddDays(-1)},
 new(){ReservationId=2,BorrowerUserId=123,Status="Approved",StartDateTime=now.AddDays(1),ExpectedReturnDateTime=now.AddDays(2)},
 new(){ReservationId=3,BorrowerUserId=123,Status="Borrowed",ExpectedReturnDateTime=now.AddDays(3)}
};
rows[0].ReservationItems.Add(new ReservationItem { IsReturned=true, Equipment=new Equipment {EquipmentName="Already returned"} });
rows[0].ReservationItems.Add(new ReservationItem { IsReturned=false, Equipment=new Equipment {EquipmentName="Still out"} });
rows[1].ReservationItems.Add(new ReservationItem { Equipment=new Equipment {EquipmentName="Projector, seminar"} });
var failed=false;var empty=false;var repositoryCalls=0;
var repository=Stub<IReservationRepository>((name,args)=> {
 repositoryCalls++;
 Check(name=="GetBorrowerDashboardReservationsAsync" && (int)args[0]==123 && args[1] is DateTime,"Wrong dashboard read or borrower identity");
 if(failed)return Task.FromException<List<Reservation>>(new InvalidOperationException("Read unavailable"));
 return Task.FromResult(empty?new List<Reservation>():rows);
});
var reservations=Stub<IReservationService>((name,args)=> {Check(name=="GetReservations","Unexpected reservation mutation"); return new ReservationListViewModel {Status=(string)args[0],Counts=new(){{"Pending",2},{"Approved",3},{"Borrowed",4},{"Overdue",1}},Items=new(){new(){ReservationId=1}}};});
var reportFailed=false;
var reports=Stub<IReportService>((name,args)=> {Check(name=="GetBorrowingReport" && ((BorrowingReportFilter)args[0]).Status=="Overdue" && (int)args[2]==4,"Wrong overdue source"); if(reportFailed)throw new Exception("Read unavailable"); return new BorrowingReportViewModel {Summary=new(){TotalRecords=2}};});
var equipment=Stub<IEquipmentService>((name,args)=> {Check(name=="GetEquipmentPage","Unexpected equipment mutation");return new EquipmentIndexViewModel{TotalEquipment=12,AvailableCount=5};});
var maintenance=Stub<IMaintenanceService>((name,args)=> {Check(name=="GetPage","Unexpected maintenance mutation");return new MaintenancePageViewModel{RecordsAvailable=true,TotalCount=(string)args[1]=="Scheduled"?3:1};});
var service=new DashboardService(repository,reservations,Stub<IUserAccountService>((_,_)=>new List<UserAccountListItem>()), Stub<IBorrowerProfileService>((_,_)=>new BorrowerPageViewModel()), reports,equipment,maintenance,NullLogger<DashboardService>.Instance);
var borrower=await service.GetBorrowerOverviewAsync(123);
Check(borrower.Reservations.Select(r=>r.ReservationId).SequenceEqual(new[]{1,2,3}),"Repository ordering changed during mapping");
var first=borrower.Reservations[0];
Check(first.EquipmentNames.SequenceEqual(new[]{"Still out"}) && first.EquipmentSummary=="Still out","Returned equipment included in outstanding summary");
Check(first.IsOverdue && !borrower.Reservations[1].IsOverdue && !borrower.Reservations[2].IsOverdue,"Due-date semantics incorrect");
Check(first.ReservationNumber=="CHECK-1" && first.PickupAt==rows[0].StartDateTime && first.ReturnBy==rows[0].ExpectedReturnDateTime,"Reservation fields lost during mapping");
Check(borrower.Reservations[1].EquipmentNames.SequenceEqual(new[]{"Projector, seminar"}),"Equipment name split on punctuation");
failed=true;
try { await service.GetBorrowerOverviewAsync(123);throw new Exception("Read failure hidden"); } catch(InvalidOperationException) { }
failed=false;empty=true;
Check((await service.GetBorrowerOverviewAsync(123)).Reservations.Count==0,"Empty query must remain empty");
var callsBefore=repositoryCalls;
try { await service.GetBorrowerOverviewAsync(0);throw new Exception("Invalid identity accepted"); } catch(ArgumentOutOfRangeException) { }
Check(repositoryCalls==callsBefore,"Invalid identity reached repository");
var desk=service.GetCustodianOverview();Check(desk.OutstandingCount==5 && desk.Count("Approved")==3 && desk.Overdue.Summary.TotalRecords==2,"Staff totals mismatch");
Check(desk.ScheduledMaintenance.TotalCount==3 && desk.OngoingMaintenance.TotalCount==1,"Maintenance states mixed");
reportFailed=true;desk=service.GetCustodianOverview();Check(desk.Overdue==null && desk.Equipment.TotalEquipment==12 && desk.Pending!=null,"Staff partial failure hid healthy sections");
var borrowerCalls=0;var adminCalls=0;var staffCalls=0;
var roleService=Stub<IDashboardService>((name,args)=> {if(name=="GetBorrowerOverviewAsync"){borrowerCalls++;Check((int)args[0]==123,"Controller accepted query identity");return Task.FromResult(borrower);}if(name=="GetAdminOverview"){adminCalls++;return new AdminDashboardViewModel();}staffCalls++;return desk;});
IActionResult Act(string role,string id=null) {
 var claims=new List<Claim>{new(ClaimTypes.Role,role)};if(id!=null)claims.Add(new(ClaimTypes.NameIdentifier,id));
 var context=new DefaultHttpContext{User=new ClaimsPrincipal(new ClaimsIdentity(claims,"checks"))};
 var controller=new DashboardController(new HttpContextAccessor{HttpContext=context},NullLoggerFactory.Instance,new ConfigurationBuilder().Build(),roleService){ControllerContext=new ControllerContext{HttpContext=context}};
 return controller.Index().GetAwaiter().GetResult();
}
Check(Act("Borrower") is ChallengeResult && borrowerCalls==0,"Missing claim must never query borrower records");
Check(((ViewResult)Act("Borrower","123")).ViewName=="Borrower" && borrowerCalls==1,"Borrower route mismatch");
Check(((ViewResult)Act("Custodian")).ViewName=="Custodian" && staffCalls==1,"Custodian route mismatch");
Check(((ViewResult)Act("Admin")).ViewName=="Admin" && adminCalls==1 && staffCalls==1,"Admin behavior changed");
Check(Act("Unknown") is ForbidResult,"Unknown role allowed");
Console.WriteLine("PASS: async dashboard reads, claim forwarding, reservation mapping/order, returned-item exclusion, due dates, failure propagation, empty results, staff section failures and role routing (fake dependencies; no database).");
public class ReadProxy:DispatchProxy {public Func<string,object[],object> Read {get;set;}protected override object Invoke(MethodInfo method,object[] args)=>Read(method.Name,args);}
