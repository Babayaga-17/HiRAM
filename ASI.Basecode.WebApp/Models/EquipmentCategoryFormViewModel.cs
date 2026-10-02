using System.ComponentModel.DataAnnotations;

namespace ASI.Basecode.WebApp.Models;

public class EquipmentCategoryFormViewModel
{
    [Required(ErrorMessage = "Category code is required.")]
    [StringLength(20, ErrorMessage = "Category code must not exceed 20 characters.")]
    public string CategoryCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Category name is required.")]
    [StringLength(100, ErrorMessage = "Category name must not exceed 100 characters.")]
    public string CategoryName { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Description must not exceed 500 characters.")]
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
