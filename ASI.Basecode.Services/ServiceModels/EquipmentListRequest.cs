namespace ASI.Basecode.Services.ServiceModels
{
    public class EquipmentListRequest
    {
        public int Page { get; set; } = 1;
        public string Search { get; set; } = string.Empty;
        public int? CategoryId { get; set; }
        public string Status { get; set; } = "all";
    }
}