namespace ASI.Basecode.Services.ServiceModels
{
    public class EquipmentCategoryListItem
    {
        public int CategoryId { get; set; }
        public string CategoryCode { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int EquipmentCount { get; set; }
        public bool IsActive { get; set; }
    }
}
