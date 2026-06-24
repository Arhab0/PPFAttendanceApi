namespace PPFAttendanceApi.Dto
{
    public class ShiftDto
    {
        public int? ShiftId { get; set; }
        public string Type { get; set; }
        public int ShiftHours { get; set; }
        public TimeOnly ShiftStartAt { get; set; }
        public TimeOnly ShiftEndAt { get; set; }
    }
}
