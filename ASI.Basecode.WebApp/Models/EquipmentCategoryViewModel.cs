namespace ASI.Basecode.WebApp.Models;

public class EquipmentCategoryViewModel
{
    public int CategoryId { get; set; }
    public string CategoryCode { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int EquipmentCount { get; set; }
    public bool IsActive { get; set; } = true;
}
