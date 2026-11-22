using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASI.Basecode.Resources.Constants
{
    public static class EquipmentPhotoPolicy
    {
        public const int MaxPhotosPerEquipment = 5;
        public const int MaxPhotoSizeMb = 5;
        public const long MaxPhotoBytes = MaxPhotoSizeMb * 1024L * 1024L;
    }
}
