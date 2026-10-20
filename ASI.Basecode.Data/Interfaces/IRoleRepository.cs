using ASI.Basecode.Data.Models;
using System.Linq;

namespace ASI.Basecode.Data.Interfaces
{
    public interface IRoleRepository
    {
        IQueryable<Role> GetRoles();
        Role GetRoleById(byte roleId);
    }
}
