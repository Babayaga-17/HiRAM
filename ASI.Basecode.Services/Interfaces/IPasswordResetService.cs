using System.Threading.Tasks;

namespace ASI.Basecode.Services.Interfaces
{
    public interface IPasswordResetService
    {
        Task<bool> SendResetCodeAsync(string email,string createdBy);
        bool ResetPassword(string email, string code, string newPassword, string updatedBy);
    }
}