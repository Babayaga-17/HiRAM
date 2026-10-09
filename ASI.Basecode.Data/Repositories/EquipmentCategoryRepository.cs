using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Exceptions;
using Basecode.Data.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace ASI.Basecode.Data.Repositories
{
    public class EquipmentCategoryRepository : BaseRepository, IEquipmentCategoryRepository
    {
        public EquipmentCategoryRepository(IUnitOfWork unitOfWork) : base(unitOfWork)
        {
        }

        public void AddEquipmentCategory(EquipmentCategory equipmentCategory)
        {
            GetDbSet<EquipmentCategory>().Add(equipmentCategory);
            UnitOfWork.SaveChanges();
        }

        public EquipmentCategory GetEquipmentCategoryById(int id)
        {
            return GetDbSet<EquipmentCategory>().AsTracking().FirstOrDefault(category => category.CategoryId == id);
        }

        public void UpdateEquipmentCategory(EquipmentCategory equipmentCategory)
        {
            GetDbSet<EquipmentCategory>().Update(equipmentCategory);
            UnitOfWork.SaveChanges();
        }

        public IQueryable<EquipmentCategory> GetEquipmentCategories()
        {
            return GetDbSet<EquipmentCategory>().AsNoTracking();
        }

        public bool DeleteEquipmentCategory(int id)
        {
            return GetDbSet<EquipmentCategory>()
                .Where(category => category.CategoryId == id && !category.IsActive && !category.Equipment.Any())
                .ExecuteDelete() == 1;
        }

        public bool CategoryCodeExists(string categoryCode, int? excludeCategoryId = null)
        {
            var query = GetDbSet<EquipmentCategory>().Where(category => category.CategoryCode == categoryCode);

            if (excludeCategoryId.HasValue)
            {
                query = query.Where(category => category.CategoryId != excludeCategoryId.Value);
            }

            return query.Any();
        }

        public bool CategoryNameExists(string categoryName, int? excludeCategoryId = null)
        {
            var query = GetDbSet<EquipmentCategory>().Where(category => category.CategoryName == categoryName);

            if (excludeCategoryId.HasValue)
            {
                query = query.Where(category => category.CategoryId != excludeCategoryId.Value);
            }

            return query.Any();
        }
    }
}
