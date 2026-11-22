using System;

namespace ASI.Basecode.Services.ServiceModels
{
    public class PasswordResetTimingViewModel
    {
        public DateTime ServerNowUtc { get; set; }
        public DateTime? ExpiresAtUtc { get; set; }
        public DateTime? ResendAvailableAtUtc { get; set; }
    }
}