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
            SaveCategoryChanges(equipmentCategory);
        }

        public EquipmentCategory GetEquipmentCategoryById(int id)
        {
            return GetDbSet<EquipmentCategory>().AsTracking().FirstOrDefault(category => category.CategoryId == id);
        }

        public void UpdateEquipmentCategory(EquipmentCategory equipmentCategory)
        {
            SaveCategoryChanges(equipmentCategory);
        }

        public IQueryable<EquipmentCategory> GetEquipmentCategories()
        {
            return GetDbSet<EquipmentCategory>().AsNoTracking();
        }

        public bool DeleteEquipmentCategory(int id)
        {
            // Check the current state in the DELETE statement, even if it changed
            // after the service checked it. Assigned equipment must be preserved.
            return GetDbSet<EquipmentCategory>()
                .Where(category => category.CategoryId == id &&
                    !category.IsActive && !category.Equipment.Any())
                .ExecuteDelete() == 1;
        }

        public bool CategoryCodeExists(string categoryCode)
        {
            return GetDbSet<EquipmentCategory>()
                .Any(category => category.CategoryCode == categoryCode);
        }

        public bool CategoryNameExists(string categoryName)
        {
            return GetDbSet<EquipmentCategory>()
                .Any(category => category.CategoryName == categoryName);
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

        private void SaveCategoryChanges(EquipmentCategory equipmentCategory)
        {
            try
            {
                UnitOfWork.SaveChanges();
            }
            // SQL Server reports duplicate unique values with error 2601 or 2627.
            // This covers another request saving the same values after our checks.
            catch (DbUpdateException exception) when (
                exception.InnerException is SqlException sqlException &&
                (sqlException.Number == 2601 || sqlException.Number == 2627))
            {
                // Stop tracking the failed insert so a later SaveChanges cannot retry it.
                Context.Entry(equipmentCategory).State = EntityState.Detached;
                throw new EquipmentCategoryConflictException(exception);
            }
        }
    }
}
