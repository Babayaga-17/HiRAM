using System.Reflection;
using System.Security.Claims;
using System.Data.Common;
using ASI.Basecode.Data;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Repositories;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Services;
using ASI.Basecode.Services.ServiceModels.Reservation;
using ASI.Basecode.WebApp.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
static T Stub<T>(Func<string,object[],object> read) where T:class { var proxy=DispatchProxy.Create<T,Proxy>();((Proxy)(object)proxy).Read=read;return proxy; }
var now=DateTime.Now;
var count=12;
var fail=false;
var requestedSkip=-1;
var requestedSize=-1;
string requestedStatus=null;
DateTime? cutoff=null;
var rows=new List<Reservation> {
 new(){ReservationId=1,BorrowerUserId=123,Status="Pending",ReservationNumber="TEST-1",ReservationItems=new List<ReservationItem>{new(){ReservationItemId=3,Equipment=new(){EquipmentName="Third"}},new(){ReservationItemId=1,Equipment=new(){EquipmentName="Projector, seminar"}},new(){ReservationItemId=2,Equipment=new(){EquipmentName="Second"}}}},
 new(){ReservationId=2,BorrowerUserId=123,Status="Borrowed",ExpectedReturnDateTime=now.AddDays(-1)},
 new(){ReservationId=3,BorrowerUserId=123,Status="Overdue",ExpectedReturnDateTime=now.AddDays(1)}
};
var repository=Stub<IReservationRepository>((name,args)=> {
 Check((int)args[0]==123,"Identity not passed to query");
 if(fail)throw new InvalidOperationException("Unavailable");
 if(name=="CountBorrowerReservationsAsync") { if(cutoff==null)cutoff=(DateTime)args[2];Check(cutoff==(DateTime)args[2],"Count cutoff changed");return Task.FromResult(count); }
 Check(name=="GetBorrowerReservationPageAsync","Unexpected write/read");
 Check(cutoff==(DateTime)args[2],"Page cutoff differs from count");
 requestedStatus=(string)args[1];requestedSkip=(int)args[3];requestedSize=(int)args[4];return Task.FromResult(rows);
});
var service=new ReservationService(repository,Stub<INotificationService>((_,_)=>throw new Exception("No notification writes expected")), null);
var page=await service.GetBorrowerReservationsAsync(123," pending ",int.MaxValue,5);
Check(page.Status=="Pending"&&page.Page==3&&requestedSkip==10&&requestedSize==5,"Normalization/clamp incorrect");
Check(page.Items[0].EquipmentNames.SequenceEqual(new[]{"Projector, seminar","Second","Third"}),"Multi-equipment names were split or reordered incorrectly");
Check(page.Items[1].Status=="Overdue"&&page.Items[2].Status=="Borrowed","Display overdue semantics differ from report");
cutoff=null;page=await service.GetBorrowerReservationsAsync(123,"invalid",-4,int.MaxValue);
Check(page.Status==null&&page.Page==1&&page.PageSize==5&&requestedSkip==0,"Invalid query values unsafe");
foreach(var size in new[]{10,20}) {cutoff=null;page=await service.GetBorrowerReservationsAsync(123,null,1,size);Check(requestedSize==size,"Page size missing");}
count=0;rows=new();cutoff=null;page=await service.GetBorrowerReservationsAsync(123,"Returned",99,5);
Check(page.Page==1&&page.FirstItem==0&&page.LastItem==0&&page.Items.Count==0,"Empty page incorrect");
try{await service.GetBorrowerReservationsAsync(0,null,1,5);throw new Exception("Invalid identity accepted");}catch(ArgumentOutOfRangeException){}
fail=true;try{await service.GetBorrowerReservationsAsync(123,null,1,5);throw new Exception("Failure disguised as empty");}catch(InvalidOperationException){}
var identityCalls=0;
var controllerService=Stub<IReservationService>((name,args)=> {identityCalls++;Check(name=="GetBorrowerReservationsAsync"&&(int)args[0]==123,"Controller identity mismatch");if(fail)throw new InvalidOperationException();return Task.FromResult(new MyReservationPageViewModel());});
ReservationController Controller(string id) {
 var claims=new List<Claim>{new(ClaimTypes.Role,"Borrower")};if(id!=null)claims.Add(new(ClaimTypes.NameIdentifier,id));
 var context=new DefaultHttpContext{User=new(new ClaimsIdentity(claims,"test"))};
 return new ReservationController(new HttpContextAccessor{HttpContext=context},NullLoggerFactory.Instance,new ConfigurationBuilder().Build(),controllerService,null,null){ControllerContext=new(){HttpContext=context}};
}
Check((await Controller(null).MyReservation()) is ChallengeResult&&(await Controller("-1").MyReservation()) is ChallengeResult&&identityCalls==0,"Invalid claims queried data");
Check(!((MyReservationPageViewModel)((ViewResult)await Controller("123").MyReservation()).Model).IsAvailable,"Read failure became successful empty");
var auth=typeof(ReservationController).GetMethod("MyReservation").GetCustomAttribute<AuthorizeAttribute>();Check(auth?.Roles=="Borrower","Borrower route restriction missing");

var borrowerContext=new DefaultHttpContext {User=new(new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,"123"),new Claim(ClaimTypes.Role,"Borrower")},"test"))};
var detailsService=Stub<IReservationService>((name,args)=> {
 Check(name=="GetReservationDetails" && args.Length==2 && (int)args[1]==123,"Borrower details must use the ownership-scoped service method");
 return (int)args[0]==1 ? new ReservationViewModel {ReservationId=1,Status="Pending"} : null;
});
var detailController=new ReservationController(new HttpContextAccessor{HttpContext=borrowerContext},NullLoggerFactory.Instance,new ConfigurationBuilder().Build(),detailsService,null,null){ControllerContext=new(){HttpContext=borrowerContext}};
detailController.Url=Stub<IUrlHelper>((name,args)=>name=="IsLocalUrl" && args[0] is string url && url.StartsWith("/") && !url.StartsWith("//"));
Check(detailController.ReservationDetails(1,"https://outside.example") is ViewResult,"Owned details did not render");
Check(detailController.ViewData["ReservationReturnUrl"]==null,"External return URL accepted");
var localReturn="/Reservation/MyReservation?status=Pending&page=2&pageSize=5";
detailController.ReservationDetails(1,localReturn);
Check((string)detailController.ViewData["ReservationReturnUrl"]==localReturn,"List context dropped");
Check(detailController.ReservationDetails(999) is NotFoundResult,"Missing or non-owned reservation must return NotFound");
var noIdentity=Controller(null);
noIdentity.Url=detailController.Url;
Check(noIdentity.ReservationDetails(1) is ChallengeResult,"Missing identity accepted for borrower details");
foreach(var action in typeof(ReservationController).GetMethods().Where(m=>new[]{"Create","Edit","Cancel","Submitted","ChangeEquipment","DiscardEdit","ReservationDetails"}.Contains(m.Name)))
 Check(action.GetCustomAttribute<AuthorizeAttribute>()?.Roles==RoleNames.Borrower,$"Borrower restriction missing on {action.Name}");
var reviewAuth=typeof(ReservationController).GetMethod("Review").GetCustomAttribute<AuthorizeAttribute>();
Check(reviewAuth!=null && reviewAuth.Roles.Split(',').Select(r=>r.Trim()).ToHashSet().SetEquals(new[]{RoleNames.Custodian,RoleNames.Admin}),"Review must be staff-only");

// SQL translation only. This context cannot open a connection and never creates/deletes a database.
using var context=new TranslationContext();
var realRepository=new ReservationRepository(new ReadOnlyUnit(context));
var queryMethod=typeof(ReservationRepository).GetMethod("BorrowerListQuery",BindingFlags.NonPublic|BindingFlags.Instance);
foreach(var status in new string[]{null,"Pending","Borrowed","Overdue"}) {
 var query=(IQueryable<Reservation>)queryMethod.Invoke(realRepository,new object[]{123,status,now});
 var sql=query.OrderByDescending(r=>r.RequestedAt).ThenByDescending(r=>r.ReservationId).Skip(5).Take(5).ToQueryString();
 Check(sql.Contains("WHERE")&&sql.Contains("borrower_user_id")&&sql.Contains("123"),"SQL ownership predicate missing");
 Check(sql.Contains("DESC")&&sql.Contains("OFFSET")&&sql.Contains("FETCH NEXT"),"SQL order/paging missing");
 if(status=="Borrowed"||status=="Overdue")Check(sql.Contains("expected_return_date_time"),"Date-aware filter missing");
}
Console.WriteLine("PASS: normalization, page bounds/sizes, multi-equipment mapping, overdue semantics, claims, unavailable state, borrower/staff action authorization, migrated details ownership/return URLs and SQL translation (no database connection).");
public class Proxy:DispatchProxy {public Func<string,object[],object> Read;protected override object Invoke(MethodInfo m,object[] a)=>Read(m.Name,a);}
class ReadOnlyUnit(DbContext context):IUnitOfWork {public DbContext Database=>context;public void SaveChanges()=>throw new InvalidOperationException("Writes prohibited");}
class TranslationContext:HiramDbContext {protected override void OnConfiguring(DbContextOptionsBuilder b)=>b.UseSqlServer("Server=127.0.0.1,1;Database=TranslationOnly;Integrated Security=True;TrustServerCertificate=True").AddInterceptors(new DenyConnections());}
class DenyConnections:DbConnectionInterceptor {public override InterceptionResult ConnectionOpening(DbConnection connection,ConnectionEventData data,InterceptionResult result)=>throw new InvalidOperationException("Database access prohibited");}
