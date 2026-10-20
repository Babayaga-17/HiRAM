using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using Basecode.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace ASI.Basecode.Data.Repositories
{
    public class RoleRepository : BaseRepository, IRoleRepository
    {
        public RoleRepository(IUnitOfWork unitOfWork) : base(unitOfWork)
        {
        }

        public IQueryable<Role> GetRoles()
        {
            return GetDbSet<Role>().AsNoTracking();
        }

        public Role GetRoleById(byte roleId)
        {
            return GetDbSet<Role>().AsNoTracking().FirstOrDefault(role => role.RoleId == roleId);
        }
    }
}
