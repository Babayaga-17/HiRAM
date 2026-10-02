using ASI.Basecode.Services.ServiceModels;
using System.Collections.Generic;

namespace ASI.Basecode.Services.Interfaces
{
    public interface IEquipmentCategoryService
    {
        void AddEquipmentCategory(string categoryCode, string categoryName, string description, bool isActive, string createdBy);
        IReadOnlyList<EquipmentCategoryListItem> GetEquipmentCategories();
        EquipmentCategoryListItem GetEquipmentCategoryById(int id);
        bool EditEquipmentCategory(int categoryId, string categoryCode, string categoryName, string description, bool isActive, string updatedBy);
    }
}
