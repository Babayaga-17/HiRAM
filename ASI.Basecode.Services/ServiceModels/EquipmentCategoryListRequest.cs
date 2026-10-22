namespace ASI.Basecode.Services.ServiceModels
{
    public class EquipmentCategoryListRequest
    {
        public int Page { get; set; } = 1;
        public string Search { get; set; } = string.Empty;
        public string Status { get; set; } = "all";
    }
}