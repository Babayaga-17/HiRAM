using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASI.Basecode.Resources.EmailTemplates
{
    public static class ReservationEmailTemplates
    {
        public static string SubmissionBody(string reservationNumber, DateTime pickup, DateTime expectedReturn)
        {
            return $"Your reservation request {reservationNumber} has been received " +
                   "and is pending approval.\n\n" +
                   $"Scheduled pickup: {pickup:MMM d, yyyy h:mm tt}\n" +
                   $"Expected return: {expectedReturn:MMM d, yyyy h:mm tt}\n\n" +
                   "All times are Philippine time (UTC+8). " +
                   "Please wait for approval before collecting the equipment.";
        }

        public static string ApprovalBody(string reservationNumber, DateTime pickup, DateTime expectedReturn)
        {
            return $"Your reservation {reservationNumber} has been approved.\n\n" +
                   $"Scheduled pickup: {pickup:MMM d, yyyy h:mm tt}\n" +
                   $"Expected return: {expectedReturn:MMM d, yyyy h:mm tt}\n\n" +
                   "All times are Philippine time (UTC+8). " +
                   "Please visit the equipment desk for pickup.";
        }

        public static string RejectionBody(string reservationNumber, string reason)
        {
            return $"Your reservation {reservationNumber} was rejected.\n\n" +
                   $"Reason: {reason?.Trim()}\n\n" +
                   "Please contact the equipment desk if you need clarification.";
        }

        public static string ReleaseBody(string reservationNumber, DateTime? releasedAt, DateTime expectedReturn)
        {
            var releaseTime = releasedAt?.ToString("MMM d, yyyy h:mm tt") ?? "Not recorded";

            return $"Equipment for reservation {reservationNumber} " +
                   "has been released and marked as borrowed.\n\n" +
                   $"Released: {releaseTime}\n" +
                   $"Expected return: {expectedReturn:MMM d, yyyy h:mm tt}\n\n" +
                   "All times are Philippine time (UTC+8). " +
                   "Please return the equipment to the desk by the expected return time.";
        }

        public static string ReturnBody(string reservationNumber, int returnedCount, int totalCount)
        {
            var outstandingCount = totalCount - returnedCount;

            var summary = outstandingCount == 0
                ? "All equipment has been returned. Your reservation is completed."
                : $"A partial return has been recorded. " +
                  $"{outstandingCount} equipment item(s) remain outstanding.";

            return $"A return was recorded for reservation {reservationNumber}.\n\n" +
                   $"{summary}\n\n" +
                   $"Total returned so far: {returnedCount} of {totalCount} equipment items.\n\n" +
                   "Please contact the equipment desk for any inspection concerns.";
        }
    }
}
