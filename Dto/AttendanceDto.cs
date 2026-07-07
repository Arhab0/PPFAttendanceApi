namespace PPFAttendanceApi.Dto
{
    public class AttendanceDto
    {
        public string Action { get; set; }
        public int sid { get; set; }
        public string? TimeInLat { get; set; }
        public string? TimeInLon { get; set; }
        public string? TimeInLocationName { get; set; }
        public DateTime? TimeInAt { get; set; }
        public DateTime? TimeInMobile { get; set; }
        public DateTime? TimeInImage { get; set; }
        public string? TimeInBy { get; set; }
        public string? TimeInType { get; set; }
        public IFormFile? AttendanceInImage { get; set; }
        public string? TimeOutLat { get; set; }
        public string? TimeOutLon { get; set; }
        public string? TimeOutLocationName { get; set; }
        public string? TimeOutBy { get; set; }
        public DateTime? TimeOutAt { get; set; }
        public DateTime? TimeOutMobile { get; set; }
        public DateTime? TimeOutImage { get; set; }
        public string? TimeOutType { get; set; }
        public IFormFile? AttendanceOutImage { get; set; }
    }
}
