namespace PPFAttendanceApi.Dto
{
    public class BranchDto
    {
        public int? BranchId { get; set; }
        public string BranchName { get; set; }
        public string Latitude { get; set; }
        public string Longitude { get; set; }
        public int Radius { get; set; }
    }
}
