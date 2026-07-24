namespace PPFAttendanceApi.Helper
{
    public class UploadDoc
    {
        // staff
        public static async Task<string> UploadStaffImage(IFormFile ufile, string userId)
        {
            return await UploadFile(ufile, Path.Combine("images", "staff", userId));
        }

        // Employee
        public static async Task<string> UploadEmployeeImage(IFormFile ufile, string employeeCode)
        {
            return await UploadFile(ufile, Path.Combine("images", "employee", employeeCode));
        }
        // Employee Uncropt
        public static async Task<string> UploadUnCropEmployeeImage(IFormFile ufile, string employeeCode)
        {
            return await UploadFile(ufile, Path.Combine("images", "employee", employeeCode, "uncrop"));
        }

        // Employee Attendance
        public static async Task<string> UploadEmployeeAttendaceImage(IFormFile ufile, string employeeCode)
        {
            return await UploadFile(ufile, Path.Combine("images", "employeeAttendance", employeeCode , DateTime.Now.ToString("dd-MMM-yyyy")));
        }
        // Staff Attendance
        public static async Task<string> UploadStaffAttendaceImage(IFormFile ufile, string userId)
        {
            return await UploadFile(ufile, Path.Combine("images", "staffAttendance", userId , DateTime.Now.ToString("dd-MMM-yyyy")));
        }

        private static async Task<string> UploadFile(IFormFile ufile, string relativeFolder)
        {
            try
            {
                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(ufile.FileName)}";
                var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relativeFolder);

                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                var filePath = Path.Combine(folderPath, fileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await ufile.CopyToAsync(fileStream);
                }

                return $"{fileName}";

            }
            catch (Exception ex)
            {
                return "x";
                //return new Exception("File upload failed: " + ex.Message).Message;
            }
        }
    }
}
