namespace PPFAttendanceApi.Dto
{
    public class DepartmentDto
    {
        public int? DepartmentId { get; set; }
        public string DepartmentName { get; set; }
        public int? ParentDepartmentId { get; set; }
        public int BranchId { get; set; }
    }
}
