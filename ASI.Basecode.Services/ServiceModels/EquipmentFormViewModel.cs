using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.ServiceModels;

public class EquipmentFormViewModel
{
    public static readonly string[] Conditions = { "Excellent", "Good", "Fair", "Damaged", "Lost" };

    public int EquipmentId { get; set; }

    [Required(ErrorMessage = "Equipment code is required.")]
    [StringLength(50, ErrorMessage = "Equipment code must not exceed 50 characters.")]
    public string EquipmentCode { get; set; } = string.Empty;

    [StringLength(50, ErrorMessage = "Asset tag must not exceed 50 characters.")]
    public string AssetTag { get; set; }

    [Required(ErrorMessage = "Equipment name is required.")]
    [StringLength(150, ErrorMessage = "Equipment name must not exceed 150 characters.")]
    public string EquipmentName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please select a category.")]
    public int? CategoryId { get; set; }

    [StringLength(100, ErrorMessage = "Brand must not exceed 100 characters.")]
    public string Brand { get; set; }

    [StringLength(100, ErrorMessage = "Model must not exceed 100 characters.")]
    public string Model { get; set; }

    [StringLength(100, ErrorMessage = "Serial number must not exceed 100 characters.")]
    public string SerialNumber { get; set; }

    [StringLength(500, ErrorMessage = "Description must not exceed 500 characters.")]
    public string Description { get; set; }

    [Required(ErrorMessage = "Condition is required.")]
    public string ConditionStatus { get; set; } = "Good";

    [StringLength(150, ErrorMessage = "Storage location must not exceed 150 characters.")]
    public string StorageLocation { get; set; }

    [DataType(DataType.Date)]
    public DateTime? AcquisitionDate { get; set; }

    [Range(0, 999999999999.99, ErrorMessage = "Acquisition cost must be zero or more.")]
    public decimal? AcquisitionCost { get; set; }

    public bool IsActive { get; set; } = true;

    public EquipmentFormViewModel() { }

    public EquipmentFormViewModel(EquipmentListItem equipment)
    {
        EquipmentId = equipment.EquipmentId;
        EquipmentCode = equipment.EquipmentCode;
        AssetTag = equipment.AssetTag;
        EquipmentName = equipment.EquipmentName;
        CategoryId = equipment.CategoryId;
        Brand = equipment.Brand;
        Model = equipment.Model;
        SerialNumber = equipment.SerialNumber;
        Description = equipment.Description;
        ConditionStatus = equipment.ConditionStatus;
        StorageLocation = equipment.StorageLocation;
        AcquisitionDate = equipment.AcquisitionDate?.ToDateTime(TimeOnly.MinValue);
        AcquisitionCost = equipment.AcquisitionCost;
        IsActive = equipment.IsActive;
    }
}