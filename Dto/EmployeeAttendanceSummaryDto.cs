namespace PPFAttendanceApi.Dto
{
    public class EmployeeAttendanceSummaryDto
    {
        public string EmployeeName { get; set; }
        public string EmployeeCode { get; set; }
        public string PhoneNumber { get; set; }
        public string RoleName { get; set; }
        public string PaymentType { get; set; }
        public string IsActive { get; set; }
        public string BranchName { get; set; }
        public string DepartmentName { get; set; }
        public int TotalScheduledHours { get; set; }
        public double TotalWorkedHours { get; set; }
        public double TotalShortHours { get; set; }
        public double TotalExcessHours { get; set; }
        public int TotalPresentDays { get; set; }
        public int TotalAbsentDays { get; set; }
        public int TotalMissingEntries { get; set; }
    }
}