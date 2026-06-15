namespace PPFAttendanceApi.Dto
{
    public class LoginRequestDto
    {
        public string Email { get; set; }
        public string Password { get; set; }
        public bool IsFromPortal { get; set; }
        public string DeviceId { get; set; }
        public string DeviceName { get; set; }
        public string DevicePlatform { get; set; }
    }
}
