using System.Threading.Tasks;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.ServiceModels;
namespace ASI.Basecode.Services.Interfaces;
public interface IMaintenanceService
{
    MaintenancePageViewModel GetPage(string search, string status, int page);
    MaintenanceRecord GetDetails(int id);
    Task<(int Id, bool EmailSent)> ScheduleAsync(MaintenanceForm form, int userId);
    Task<bool> UpdateAsync(MaintenanceUpdateForm form, int userId, bool isAdmin);
}
