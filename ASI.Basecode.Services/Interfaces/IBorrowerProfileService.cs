using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.ServiceModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Interfaces
{
    public interface IBorrowerProfileService
    {
        BorrowerPageViewModel GetPage(string search, string status, int page, int pageSize);
        List<Department> GetDepartments();
        BorrowerHistoryViewModel GetHistory(int userId);
        (bool ok, string message) Save(BorrowerFormModel m, string actor);
        (bool ok, string message) SetStatus(int userId, bool suspend, string reason, DateTime? until, string actor);
        (bool ok, string message) Delete(int userId);
        Task<bool> RegisterAsync(BorrowerRegistrationViewModel model, Stream photo, string fileName, CancellationToken cancellationToken = default);

    }
}
