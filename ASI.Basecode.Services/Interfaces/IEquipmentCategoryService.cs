using ASI.Basecode.Services.ServiceModels;
using System.Collections.Generic;

namespace ASI.Basecode.Services.Interfaces
{
    public interface IEquipmentCategoryService
    {
        void AddEquipmentCategory(EquipmentCategoryFormViewModel model, string createdBy);
        List<EquipmentCategoryListItem> GetEquipmentCategories();
        EquipmentCategoryListItem GetEquipmentCategoryById(int id);
        bool EditEquipmentCategory(EquipmentCategoryFormViewModel model, string updatedBy);
        bool SetEquipmentCategoryStatus(int categoryId, bool isActive, string updatedBy);
        bool DeleteEquipmentCategory(int categoryId);
    }
}
