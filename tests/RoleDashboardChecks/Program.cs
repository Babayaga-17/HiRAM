using System.Reflection;
using System.Security.Claims;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Services.Services;
using ASI.Basecode.WebApp.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

static T Stub<T>(Func<string, object[], object> read) where T : class { var proxy=DispatchProxy.Create<T, ReadProxy>(); ((ReadProxy)(object)proxy).Read=read; return proxy; }
static void Check(bool condition, string message) { if(!condition) throw new Exception(message); }
var now=DateTime.Now;
var statuses=new[]{"Pending","Approved","Approved","Borrowed","Borrowed","Overdue","Returned"};
var rows=statuses.Select((s,i)=>new Reservation {ReservationId=i+1,BorrowerUserId=123,ReservationNumber=$"CHECK-{i}",Status=s,RequestedAt=now.AddDays(-i),StartDateTime=now.AddDays(8-i),ExpectedReturnDateTime=now.AddDays(i>=4?-i:2+i)}).ToList();
rows[3].ReservationItems.Add(new ReservationItem { IsReturned=true, Equipment=new Equipment {EquipmentName="Already returned"} });
rows[3].ReservationItems.Add(new ReservationItem { IsReturned=false, Equipment=new Equipment {EquipmentName="Still out"} });
rows.Add(new Reservation {ReservationId=999,BorrowerUserId=999,Status="Borrowed",ExpectedReturnDateTime=now.AddDays(-1)});
var failed=false;var empty=false;
var repository=Stub<IEquipmentReservationRepository>((name,args)=> {
 Check((int)args[0]==123,"Borrower ID must come from signed-in identity");
 if(name=="GetBorrowerProfile") return new BorrowerProfile {UserId=123,EligibilityStatus="Eligible",BorrowerType="Student"};
 Check(name=="GetBorrowerReservations","Unexpected repository mutation");
 if(failed)throw new InvalidOperationException("Read unavailable");
 return empty?new List<Reservation>():rows;
});
var reservations=Stub<IReservationService>((name,args)=> {Check(name=="GetReservations","Unexpected reservation mutation"); return new ReservationListViewModel {Status=(string)args[0],Counts=new(){{"Pending",2},{"Approved",3},{"Borrowed",4},{"Overdue",1}},Items=new(){new(){ReservationId=1}}};});
var reportFailed=false;
var reports=Stub<IReportService>((name,args)=> {Check(name=="GetBorrowingReport" && ((BorrowingReportFilter)args[0]).Status=="Overdue" && (int)args[2]==4,"Wrong overdue source"); if(reportFailed)throw new Exception("Read unavailable"); return new BorrowingReportViewModel {Summary=new(){TotalRecords=2}};});
var equipment=Stub<IEquipmentService>((name,args)=> {Check(name=="GetEquipmentPage","Unexpected equipment mutation");return new EquipmentIndexViewModel{TotalEquipment=12,AvailableCount=5};});
var maintenance=Stub<IMaintenanceService>((name,args)=> {Check(name=="GetPage","Unexpected maintenance mutation");return new MaintenancePageViewModel{RecordsAvailable=true,TotalCount=(string)args[1]=="Scheduled"?3:1};});
var service=new DashboardService(Stub<IUserAccountService>((_,_)=>new List<UserAccountListItem>()), Stub<IBorrowerProfileService>((_,_)=>new BorrowerPageViewModel()), repository,reservations,reports,equipment,maintenance,NullLogger<DashboardService>.Instance);
var borrower=service.GetBorrowerOverview(123);
Check(borrower.Reservations.Count==7 && borrower.Reservations.All(r=>r.ReservationId!=999),"Other borrower's records leaked");
Check(borrower.Reservations.Single(r=>r.ReservationId==4).EquipmentSummary=="Still out","Returned items must not be requested again");
Check(borrower.OutstandingCount==3 && borrower.OverdueCount==2,"Outstanding/overdue definition mismatch");
Check(borrower.Count("Approved")==2 && borrower.Loans.Select(r=>r.ReturnBy).SequenceEqual(borrower.Loans.Select(r=>r.ReturnBy).OrderBy(x=>x)),"Pickup or return ordering mismatch");
Check(borrower.Recent.Count()==4 && borrower.ProfileLoaded && borrower.EligibilityStatus=="Eligible","Preview/profile mismatch");
Check(borrower.Reservations.Single(r=>r.ReservationId==4).EquipmentNames.SequenceEqual(new[]{"Still out"}),"Structured names must exclude returned items");
var attentionModel = new BorrowerDashboardViewModel { Reservations = new() {
 new() {ReservationId=10, Status="Pending"}, new() {ReservationId=11, Status="Returned"},
 new() {ReservationId=9, Status="Borrowed", ReturnBy=now.AddDays(1)},
 new() {ReservationId=4, Status="Approved", PickupAt=now.AddDays(2)},
 new() {ReservationId=3, Status="Approved", PickupAt=now.AddDays(2)},
 new() {ReservationId=2, Status="Borrowed", IsOverdue=true, ReturnBy=now.AddDays(-1)},
 new() {ReservationId=1, Status="Overdue", IsOverdue=true, ReturnBy=now.AddDays(-2)}
}};
Check(attentionModel.Attention.Select(r=>r.ReservationId).SequenceEqual(new[]{1,2,3}),"Priority, date, tie-breaker, or three-card cap changed");
attentionModel.Reservations.RemoveAll(r=>r.IsOverdue);
Check(attentionModel.Attention.Select(r=>r.ReservationId).SequenceEqual(new[]{3,4,9}),"Approved pickups must precede outstanding returns and exclude history/pending");
attentionModel.Reservations=null; Check(!attentionModel.Attention.Any(),"Unavailable preview must be safe to enumerate");
attentionModel.Reservations=new(); Check(!attentionModel.Attention.Any(),"Empty preview must stay empty");
failed=true;borrower=service.GetBorrowerOverview(123);Check(borrower.Reservations==null && borrower.OutstandingCount==null && borrower.ProfileLoaded,"Failure must stay unavailable without hiding profile");
failed=false;empty=true;borrower=service.GetBorrowerOverview(123);Check(borrower.OutstandingCount==0 && borrower.OverdueCount==0,"Genuine zero became unavailable");
try{service.GetBorrowerOverview(0);throw new Exception("Invalid identity accepted");}catch(ArgumentOutOfRangeException){}
var desk=service.GetCustodianOverview();Check(desk.OutstandingCount==5 && desk.Count("Approved")==3 && desk.Overdue.Summary.TotalRecords==2,"Staff totals mismatch");
Check(desk.ScheduledMaintenance.TotalCount==3 && desk.OngoingMaintenance.TotalCount==1,"Maintenance states mixed");
reportFailed=true;desk=service.GetCustodianOverview();Check(desk.Overdue==null && desk.Equipment.TotalEquipment==12 && desk.Pending!=null,"Staff partial failure hid healthy sections");
var borrowerCalls=0;var adminCalls=0;var staffCalls=0;
var roleService=Stub<IDashboardService>((name,args)=> {if(name=="GetBorrowerOverview"){borrowerCalls++;Check((int)args[0]==123,"Controller accepted query identity");return borrower;}if(name=="GetAdminOverview"){adminCalls++;return new AdminDashboardViewModel();}staffCalls++;return desk;});
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
Console.WriteLine("PASS: borrower claim scoping, defensive ownership, due-date semantics, ordering/caps, zero vs unavailable, section failures, read-only composition and all role routing.");
public class ReadProxy:DispatchProxy {public Func<string,object[],object> Read {get;set;}protected override object Invoke(MethodInfo method,object[] args)=>Read(method.Name,args);}
