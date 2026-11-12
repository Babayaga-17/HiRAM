using ASI.Basecode.Services.ServiceModels.Dashboard;
using System.Threading.Tasks;
namespace ASI.Basecode.Services.Interfaces;
public interface IDashboardService
{
    Task<BorrowerDashboardViewModel> GetBorrowerOverviewAsync(int borrowerUserId);
    Task<CustodianDashboardViewModel> GetCustodianOverviewAsync();
    Task<AdminDashboardViewModel> GetAdminOverviewAsync();
}
