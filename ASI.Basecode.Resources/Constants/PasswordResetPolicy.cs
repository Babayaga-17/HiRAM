using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASI.Basecode.Resources.Constants
{
    public static class PasswordResetPolicy
    {
        public const int CodeExpiryMinutes = 5;
        public const int ResendCooldownSeconds = 60;
    }
}
