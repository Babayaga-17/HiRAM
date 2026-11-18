namespace ASI.Basecode.Resources.Constants
{
    public static class ReservationPolicy
    {
        public const int MaxEquipmentPerReservation = 3;
        public const int MaxCalendarDays = 3;
        public const int MinimumBorrowingMinutes = 60;
        public const int InspectionBufferMinutes = 15;

        public static readonly TimeSpan DeskOpeningTime = new TimeSpan(8, 0, 0);
        public static readonly TimeSpan DeskClosingTime = new TimeSpan(17, 0, 0);

        public static TimeSpan LatestReturnTime => DeskClosingTime - TimeSpan.FromMinutes(InspectionBufferMinutes);
    }
}
