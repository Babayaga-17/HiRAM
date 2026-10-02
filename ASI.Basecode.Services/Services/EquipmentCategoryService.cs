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
        private readonly IEquipmentCategoryRepository _repository;

        public EquipmentCategoryService(IEquipmentCategoryRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public void AddEquipmentCategory(string categoryCode, string categoryName, string description, bool isActive, string createdBy)
        {
            categoryCode = categoryCode?.Trim();
            categoryName = categoryName?.Trim();
            description = description?.Trim();

            // Form messages belong to the ViewModel. These guards protect other callers.
            ArgumentException.ThrowIfNullOrWhiteSpace(categoryCode);
            ArgumentException.ThrowIfNullOrWhiteSpace(categoryName);
            ArgumentException.ThrowIfNullOrWhiteSpace(createdBy);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(categoryCode.Length, 20, nameof(categoryCode));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(categoryName.Length, 100, nameof(categoryName));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(createdBy.Length, 100, nameof(createdBy));

            if (description != null)
                ArgumentOutOfRangeException.ThrowIfGreaterThan(description.Length, 500, nameof(description));

            var errors = GetDuplicateErrors(categoryCode, categoryName);

            if (errors.Count > 0)
                throw new EquipmentCategoryValidationException(errors);

            var equipmentCategory = new EquipmentCategory
            {
                CategoryCode = categoryCode,
                CategoryName = categoryName,
                Description = string.IsNullOrEmpty(description) ? null : description,
                IsActive = isActive,
                CreatedBy = createdBy
            };

            try
            {
                _repository.AddEquipmentCategory(equipmentCategory);
            }
            catch (EquipmentCategoryConflictException)
            {
                // Another request may have saved these values after our first check.
                errors = GetDuplicateErrors(categoryCode, categoryName);

                if (errors.Count == 0)
                {
                    errors.Add(string.Empty, "The category conflicts with a value already saved. Please check the code and name and try again.");
                }

                throw new EquipmentCategoryValidationException(errors);
            }
        }

        public bool EditEquipmentCategory(int categoryId, string categoryCode, string categoryName, string description, bool isActive, string updatedBy)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(categoryId);
            ArgumentException.ThrowIfNullOrWhiteSpace(categoryCode);
            ArgumentException.ThrowIfNullOrWhiteSpace(categoryName);
            ArgumentException.ThrowIfNullOrWhiteSpace(updatedBy);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(categoryCode.Length, 20, nameof(categoryCode));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(categoryName.Length, 100, nameof(categoryName));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(updatedBy.Length, 100, nameof(updatedBy));

            if (description != null)
                ArgumentOutOfRangeException.ThrowIfGreaterThan(description.Length, 500, nameof(description));

            categoryCode = categoryCode?.Trim();
            categoryName = categoryName?.Trim();
            description = description?.Trim();

            // Apply the same input guards used in Add.
            // Validate updatedBy using the same rules as createdBy.

            var category = _repository.GetEquipmentCategoryById(categoryId);

            if (category == null)
                return false;

            var errors = GetDuplicateErrors(categoryCode, categoryName, categoryId);

            if (errors.Count > 0)
                throw new EquipmentCategoryValidationException(errors);

            category.CategoryCode = categoryCode;
            category.CategoryName = categoryName;
            category.Description = string.IsNullOrEmpty(description) ? null : description;
            category.IsActive = isActive;
            category.UpdatedBy = updatedBy;
            category.UpdatedAt = DateTime.Now;

            try
            {
                _repository.UpdateEquipmentCategory(category);
            }
            catch (EquipmentCategoryConflictException)
            {
                errors = GetDuplicateErrors(categoryCode, categoryName, categoryId);

                if (errors.Count == 0)
                    throw;

                throw new EquipmentCategoryValidationException(errors);
            }

            return true;
        }

        public IReadOnlyList<EquipmentCategoryListItem> GetEquipmentCategories()
        {
            return _repository.GetEquipmentCategories()
                .OrderBy(category => category.CategoryName)
                .Select(category => new EquipmentCategoryListItem
                {
                    CategoryId = category.CategoryId,
                    CategoryCode = category.CategoryCode,
                    CategoryName = category.CategoryName,
                    Description = category.Description ?? string.Empty,
                    EquipmentCount = category.Equipment.Count,
                    IsActive = category.IsActive
                })
                .ToList();
        }

        public EquipmentCategoryListItem GetEquipmentCategoryById(int id)
        {
            return _repository.GetEquipmentCategories()
                .Where(category => category.CategoryId == id)
                .Select(category => new EquipmentCategoryListItem
                {
                    CategoryId = category.CategoryId,
                    CategoryCode = category.CategoryCode,
                    CategoryName = category.CategoryName,
                    Description = category.Description ?? string.Empty,
                    EquipmentCount = category.Equipment.Count,
                    IsActive = category.IsActive
                })
                .SingleOrDefault();
        }

        private Dictionary<string, string> GetDuplicateErrors(string categoryCode, string categoryName, int? excludeCategoryId = null)
        {
            var errors = new Dictionary<string, string>();

            if (_repository.CategoryCodeExists(categoryCode, excludeCategoryId))
            {
                errors.Add(
                    nameof(EquipmentCategory.CategoryCode),
                    ErrorMessages.CategoryCodeExists);
            }

            if (_repository.CategoryNameExists(categoryName, excludeCategoryId))
            {
                errors.Add(
                    nameof(EquipmentCategory.CategoryName),
                    ErrorMessages.CategoryNameExists);
            }

            return errors;
        }
    }
}
