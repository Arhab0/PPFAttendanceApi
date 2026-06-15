namespace PPFAttendanceApi.Dto
{
    public class RegisterUserDto
    {
        public int? sid { get; set; }
        public string Name { get; set; }
        public string? FatherName { get; set; }
        public string? Cnic { get; set; }
        public string? MobileNumber { get; set; }
        public string? JobTitle { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public int RoleId { get; set; }
        public int? PaymentTypeId { get; set; }
        public int? ShiftTypeId { get; set; }
        public int EmployeeTypeId { get; set; }
        public List<int>? BranchIds { get; set; } = new();
        public List<int>? DepartmentIds { get; set; } = new();
    }
}
