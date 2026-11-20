using ASI.Basecode.Services.ServiceModels;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Interfaces
{
    public interface IAccountService
    {
        LoginResponse AuthenticateAccount(string email, string password);
        Task RecordSuccessfulLoginAsync(int accountId);
        Task<MyProfileViewModel> GetProfileAsync(int userId, CancellationToken cancellationToken = default);
    }
}
