using ASI.Basecode.Services.ServiceModels;
using System.Collections.Generic;

namespace ASI.Basecode.Services.Interfaces
{
    /// <summary>
    /// WBS 5.1 User and Role Management.
    /// Methods that return bool return false when the user no longer exists.
    /// Rule violations throw UserAccountValidationException.
    /// </summary>
    public interface IUserAccountService
    {
        List<UserAccountListItem> GetUserAccounts();
        UserAccountListItem GetUserAccountById(int userId);
        List<RoleListItem> GetRoles();
        int AddUserAccount(UserAccountFormViewModel model, int currentUserId, string createdBy, string ipAddress);
        bool EditUserAccount(UserAccountFormViewModel model, int currentUserId, string updatedBy, string ipAddress);
        bool SetUserAccountStatus(int userId, bool activate, string version, int currentUserId, string updatedBy, string ipAddress);
        string ResetUserAccountAccess(int userId, int currentUserId, string updatedBy, string ipAddress);
    }
}
