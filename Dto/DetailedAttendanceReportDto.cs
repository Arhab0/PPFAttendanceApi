namespace PPFAttendanceApi.Dto
{
    public class DetailedAttendanceReportDto
    {
        public string EmployeeName { get; set; }
        public string EmployeeCode { get; set; }
        public DateOnly AttendanceDate { get; set; }
        public DateTime? TimeIn { get; set; }
        public DateTime? TimeOut { get; set; }
        public int ScheduledWorkingHours { get; set; }
        public int WorkedHours { get; set; }
        public int Difference {  get; set; }
    }
}