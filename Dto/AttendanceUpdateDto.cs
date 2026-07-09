namespace PPFAttendanceApi.Dto
{
    public class AttendanceUpdateDto
    {
        public int AttendanceLogId { get; set; }
        public DateTime TimeIn { get; set; }
        public DateTime TimeOut { get; set; }
    }
}
