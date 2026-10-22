using System;
using System.Collections.Generic;

namespace ASI.Basecode.Services.ServiceModels
{
    public class EquipmentCategoryIndexViewModel
    {
        public List<EquipmentCategoryListItem> Items { get; set; } = new List<EquipmentCategoryListItem>();

        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 5;
        public int FilteredCount { get; set; }

        public string Search { get; set; } = string.Empty;
        public string Status { get; set; } = "all";

        // Global overview cards, independent of the current page.
        public int TotalCategories { get; set; }
        public int ActiveCategories { get; set; }
        public int TotalEquipment { get; set; }

        public int TotalPages => Math.Max(1, (int)Math.Ceiling((double)FilteredCount / PageSize));

        public int FirstItem => FilteredCount == 0 ? 0 : (Page - 1) * PageSize + 1;

        public int LastItem => FilteredCount == 0 ? 0 : FirstItem + Items.Count - 1;
    }
}