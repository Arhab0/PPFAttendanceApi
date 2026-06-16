namespace PPFAttendanceApi.Dto
{
    public class UserLoginDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public int RoleId { get; set; }
        public string RoleName { get; set; }
        public FileDto File { get; set; }
        public List<UserLocationDto> Locations_ { get; set; } = new List<UserLocationDto>();
        public List<RegisteredEmployeesData>? Employees { get; set; } = new List<RegisteredEmployeesData>();
        public List<BranchDeptMapping> mapping { get; set; } = new();
    }
    public class FileDto
    {
        public int Sid { get; set; }
        public int FileId { get; set; }
        public string FilePath { get; set; }
        public string Extension { get; set; }
    }
    public class UserLocationDto
    {
        public int LocationId { get; set; }
        public string Latitude { get; set; }
        public string Longitude { get; set; }
        public string LocationType { get; set; }
        public int Radius { get; set; }
    }
    public class RegisteredEmployeesData
    {
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; }
        public string EmployeeEmail { get; set; }
        public string EmployeeCode { get; set; }
        public string MobileNumber { get; set; }
        public int RoleId { get; set; }
        public string RoleName { get; set; }
        public int DepartmentId { get; set; }
        public string DepartmentName { get; set; }
        public FileDto File { get; set; }
        public List<UserLocationDto> Locations_ { get; set; } = new List<UserLocationDto>();
        public List<BranchDeptMapping> mapping { get; set; } = new();
    }

    public class BranchDeptMapping
    {
        public int MappingId { get; set; }
        public int BranchId { get; set; }
        public string BranchName { get; set; }
        public int? DepartmentId { get; set; }
        public string? DepartmentName { get; set; }
    }
}
