using ASI.Basecode.Services.ServiceModels;
namespace ASI.Basecode.Services.Interfaces;
public interface IDashboardService
{
    BorrowerDashboardViewModel GetBorrowerOverview(int borrowerUserId);
    CustodianDashboardViewModel GetCustodianOverview();
    AdminDashboardViewModel GetAdminOverview();
}
