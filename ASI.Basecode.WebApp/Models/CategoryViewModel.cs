namespace ASI.Basecode.WebApp.Models;

public class CategoryViewModel
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int EquipmentCount { get; set; }
    public bool IsActive { get; set; } = true;
}
