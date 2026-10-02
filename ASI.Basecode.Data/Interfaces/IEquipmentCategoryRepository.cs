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
        bool CategoryCodeExists(string categoryCode);
        bool CategoryNameExists(string categoryName);
    }
}
