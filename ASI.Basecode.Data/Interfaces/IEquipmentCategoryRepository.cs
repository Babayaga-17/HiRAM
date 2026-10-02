using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ASI.Basecode.Data.Models;

namespace ASI.Basecode.Data.Interfaces
{
    public interface IEquipmentCategoryRepository
    {
        void AddEquipmentCategory(EquipmentCategory equipmentCategory);
        IQueryable<EquipmentCategory> GetEquipmentCategories();
        EquipmentCategory GetEquipmentCategoryById(int id);
        void UpdateEquipmentCategory(EquipmentCategory equipmentCategory);
        bool CategoryCodeExists(string categoryCode, int? excludeCategoryId = null);
        bool CategoryNameExists(string categoryName, int? excludeCategoryId = null);
    }
}
