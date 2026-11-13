using ASI.Basecode.Data.Exceptions;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Exceptions;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using ErrorMessages = ASI.Basecode.Resources.Messages.Errors;

namespace ASI.Basecode.Services.Services
{
    public class EquipmentCategoryService : IEquipmentCategoryService
    {
        private readonly IEquipmentCategoryRepository _equipmentCategoryRepository;

        // One shared mapping used by every list/detail query. It is an Expression so EF Core still translates it to SQL.
        private static readonly Expression<Func<EquipmentCategory, EquipmentCategoryListItem>> ToListItem = category => new EquipmentCategoryListItem
        {
            CategoryId = category.CategoryId,
            CategoryCode = category.CategoryCode,
            CategoryName = category.CategoryName,
            Description = category.Description ?? string.Empty,
            EquipmentCount = category.Equipment.Count,
            IsActive = category.IsActive
        };

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
                Description = model.Description,
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

            return _equipmentCategoryRepository.DeleteEquipmentCategory(categoryId);
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
            return _equipmentCategoryRepository.GetEquipmentCategories().Select(ToListItem).ToList();
        }

        public EquipmentCategoryListItem GetEquipmentCategoryById(int id)
        {
            return _equipmentCategoryRepository.GetEquipmentCategories()
                .Where(equipmentCategory => equipmentCategory.CategoryId == id)
                .Select(ToListItem)
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

        public EquipmentCategoryIndexViewModel GetEquipmentCategoryPage(EquipmentCategoryListRequest request)
        {
            const int pageSize = 5;

            var search = (request.Search ?? string.Empty).Trim();
            var page = request.Page;

            var status = request.Status == "active" ||
                         request.Status == "inactive"
                ? request.Status
                : "all";

            // This remains IQueryable, so filtering and pagination run in SQL.
            var allCategories = _equipmentCategoryRepository.GetEquipmentCategories();
            var query = allCategories;

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(category =>
                    category.CategoryName.Contains(search) ||
                    category.CategoryCode.Contains(search) ||
                    (category.Description != null && category.Description.Contains(search)));
            }

            if (status != "all")
            {
                var isActive = status == "active";
                query = query.Where(category => category.IsActive == isActive);
            }

            var filteredCount = query.Count();
            var totalPages = Math.Max(1, (int)Math.Ceiling((double)filteredCount / pageSize));

            // Handles negative pages and pages beyond the last result.
            page = Math.Clamp(page, 1, totalPages);

            var items = query
                .OrderBy(category => category.CategoryName)
                .ThenBy(category => category.CategoryId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(ToListItem)
                .ToList();

            return new EquipmentCategoryIndexViewModel
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                FilteredCount = filteredCount,
                Search = search,
                Status = status,

                TotalCategories = allCategories.Count(),
                ActiveCategories = allCategories.Count(category => category.IsActive),
                TotalEquipment = allCategories
                    .Select(category => (int?)category.Equipment.Count)
                    .Sum() ?? 0
            };
        }
    }
}
