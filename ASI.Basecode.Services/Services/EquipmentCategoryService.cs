using ASI.Basecode.Data.Exceptions;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Exceptions;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using System;
using System.Collections.Generic;
using System.Linq;
using ErrorMessages = ASI.Basecode.Resources.Messages.Errors;

namespace ASI.Basecode.Services.Services
{
    public class EquipmentCategoryService : IEquipmentCategoryService
    {
        private readonly IEquipmentCategoryRepository _equipmentCategoryRepository;

        public EquipmentCategoryService(IEquipmentCategoryRepository equipmentCategoryRepository)
        {
            _equipmentCategoryRepository = equipmentCategoryRepository;
        }

        public void AddEquipmentCategory(EquipmentCategoryFormViewModel model, string createdBy)
        {
            var errors = GetDuplicateErrors(model.CategoryCode, model.CategoryName);

            if (errors.Count > 0)
                throw new EquipmentCategoryValidationException(errors);

            var equipmentCategory = new EquipmentCategory
            {
                CategoryCode = model.CategoryCode,
                CategoryName = model.CategoryName,
                Description = model.Description ?? null,
                IsActive = model.IsActive,
                CreatedBy = createdBy
            };

            _equipmentCategoryRepository.AddEquipmentCategory(equipmentCategory);
        }

        public bool EditEquipmentCategory(EquipmentCategoryFormViewModel model, string updatedBy)
        {
            var equipmentCategory = _equipmentCategoryRepository.GetEquipmentCategoryById(model.CategoryId);

            if (equipmentCategory == null)
                return false;

            var errors = GetDuplicateErrors(model.CategoryCode, model.CategoryName, model.CategoryId);

            if (errors.Count > 0)
                throw new EquipmentCategoryValidationException(errors);

            equipmentCategory.CategoryCode = model.CategoryCode;
            equipmentCategory.CategoryName = model.CategoryName;
            equipmentCategory.Description = string.IsNullOrEmpty(model.Description) ? null : model.Description;
            equipmentCategory.IsActive = model.IsActive;
            equipmentCategory.UpdatedBy = updatedBy;
            equipmentCategory.UpdatedAt = DateTime.Now;

            _equipmentCategoryRepository.UpdateEquipmentCategory(equipmentCategory);

            return true;
        }

        public bool SetEquipmentCategoryStatus(int categoryId, bool isActive, string updatedBy)
        {
            var equipmentCategory = _equipmentCategoryRepository.GetEquipmentCategoryById(categoryId);

            if (equipmentCategory == null)
                return false;

            if (equipmentCategory.IsActive == isActive)
                return true;

            equipmentCategory.IsActive = isActive;
            equipmentCategory.UpdatedBy = updatedBy;
            equipmentCategory.UpdatedAt = DateTime.Now;
            _equipmentCategoryRepository.UpdateEquipmentCategory(equipmentCategory);

            return true;
        }

        public bool DeleteEquipmentCategory(int categoryId)
        {
            var equipmentCategory = GetEquipmentCategoryById(categoryId);

            if (equipmentCategory == null)
                return false;

            ValidateCategoryDeletion(equipmentCategory);

            if (_equipmentCategoryRepository.DeleteEquipmentCategory(categoryId))
                return true;

            return false;
        }

        private static void ValidateCategoryDeletion(EquipmentCategoryListItem equipmentCategory)
        {
            if (equipmentCategory.IsActive)
            {
                throw new EquipmentCategoryValidationException(new Dictionary<string, string>
                {
                    [string.Empty] = "Deactivate the category before deleting it."
                });
            }

            if (equipmentCategory.EquipmentCount > 0)
            {
                throw new EquipmentCategoryValidationException(new Dictionary<string, string>
                {
                    [string.Empty] = "This category has assigned equipment. Reassign that equipment before deleting the category."
                });
            }
        }

        public List<EquipmentCategoryListItem> GetEquipmentCategories()
        {
            return _equipmentCategoryRepository.GetEquipmentCategories().Select(equipmentCategory => new EquipmentCategoryListItem
                {
                    CategoryId = equipmentCategory.CategoryId,
                    CategoryCode = equipmentCategory.CategoryCode,
                    CategoryName = equipmentCategory.CategoryName,
                    Description = equipmentCategory.Description ?? string.Empty,
                    EquipmentCount = equipmentCategory.Equipment.Count,
                    IsActive = equipmentCategory.IsActive
                }).ToList();
        }

        public EquipmentCategoryListItem GetEquipmentCategoryById(int id)
        {
            return _equipmentCategoryRepository.GetEquipmentCategories()
                .Where(equipmentCategory => equipmentCategory.CategoryId == id)
                .Select(equipmentCategory => new EquipmentCategoryListItem
                {
                    CategoryId = equipmentCategory.CategoryId,
                    CategoryCode = equipmentCategory.CategoryCode,
                    CategoryName = equipmentCategory.CategoryName,
                    Description = equipmentCategory.Description ?? string.Empty,
                    EquipmentCount = equipmentCategory.Equipment.Count,
                    IsActive = equipmentCategory.IsActive
                })
                .SingleOrDefault();
        }

        private Dictionary<string, string> GetDuplicateErrors(string categoryCode, string categoryName, int? excludeCategoryId = null)
        {
            var errors = new Dictionary<string, string>();

            if (_equipmentCategoryRepository.CategoryCodeExists(categoryCode, excludeCategoryId))
            {
                errors.Add(nameof(EquipmentCategory.CategoryCode), ErrorMessages.CategoryCodeExists);
            }

            if (_equipmentCategoryRepository.CategoryNameExists(categoryName, excludeCategoryId))
            {
                errors.Add(nameof(EquipmentCategory.CategoryName), ErrorMessages.CategoryNameExists);
            }

            return errors;
        }
    }
}
