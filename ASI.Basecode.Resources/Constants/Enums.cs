namespace ASI.Basecode.Resources.Constants
{
    /// <summary>
    /// Class for enumerated values
    /// </summary>
    public class Enums
    {
        /// <summary>
        /// API Result Status
        /// </summary>
        public enum Status
        {
            Success,
            Error,
            CustomErr,
        }

        /// <summary>
        /// Login Result
        /// </summary>
        public enum LoginResult
        {
            Success = 0,
            Failed = 1,
        }

        public enum LoginFailureReason
        {
            InvalidCredentials,
            PendingApproval,
            Inactive,
            Suspended,
            Disabled,
            TemporarilyLocked
        }

        public enum SendResetCodeStatus
        {
            Sent,
            AccountNotFound,
            Cooldown,
            Failed
        }
    }
}
