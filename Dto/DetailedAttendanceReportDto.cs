namespace PPFAttendanceApi.Dto
{
    public class DetailedAttendanceReportDto
    {
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; }
        public string EmployeeCode { get; set; }
        public string PhoneNumber { get; set; }
        public string RoleName { get; set; }
        public string PaymentType { get; set; }
        public string IsActive { get; set; }
        public DateOnly AttendanceDate { get; set; } = new DateOnly();
        public DateTime? TimeIn { get; set; }
        public DateTime? TimeOut { get; set; }
        public string PresentStatus { get; set; }
        public int ScheduledWorkingHours { get; set; }
        public string WorkedHours { get; set; }
        public double Difference {  get; set; }
    }
}