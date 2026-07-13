namespace PPFAttendanceApi.Dto
{
    public class BranchDashboardDto
    {
        public int BranchId { get; set; }
        public string BranchName { get; set; }
        public int TotalStaffCount { get; set; }
        public int PresentTodayCount { get; set; }
        public int CheckInCount { get; set; }
        public int CheckOutCount { get; set; }
    }
}
