using System.Threading.Tasks;

namespace ASI.Basecode.Services.Interfaces
{
    public interface IPasswordResetService
    {
        /// <summary>Returns false when no account has this email.</summary>
        Task<bool> SendResetCodeAsync(string email);

        /// <summary>Step 1: checks the code only. Returns the token id when valid, otherwise null.</summary>
        int? VerifyCode(string email, string code);

        /// <summary>Step 2: sets the new password for a token that was verified in step 1.</summary>
        bool SetNewPassword(string email, int tokenId, string newPassword);
    }
}
