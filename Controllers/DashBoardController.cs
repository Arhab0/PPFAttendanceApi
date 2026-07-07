using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PPFAttendanceApi.Helper;
using PPFAttendanceApi.Models;
using System.Security.Claims;

namespace PPFAttendanceApi.Controllers
{
    [Route("api/[controller]")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class DashBoardController(ppfdbContext _context, ClaimsService _claims) : Controller
    {
        private readonly ppfdbContext db = _context;
        private readonly ClaimsService claims = _claims;

        [HttpGet("GetDashboardData")]
        public async Task<IActionResult> GetDashboardData(
                                        string filter = "today",
                                        DateTime? date = null,
                                        int branchId = 0,
                                        int departmentId = 0,
                                        int Page = 1,
                                        int PageSize = 10
                                                         )
        {
            try
            {
                var roleId = int.Parse(claims["RoleId"]);
                var sid = int.Parse(claims["sid"]);

                var today = DateTime.Now.Date;
                var tomorrow = today.AddDays(1);

                DateTime rangeStart, rangeEnd;

                switch (filter.ToLower().Trim())
                {
                    case "yesterday":
                        rangeStart = today.AddDays(-1);
                        rangeEnd = today;
                        break;

                    case "date":
                        if (date == null)
                            return BadRequest("'date' is required for filter.");
                        rangeStart = date.Value.Date;
                        rangeEnd = date.Value.Date.AddDays(1);
                        break;

                    default: // "today"
                        rangeStart = today;
                        rangeEnd = tomorrow;
                        break;
                }

                var employeeQuery = db.Employees.AsNoTracking()
                    .Where(e => e.IsActive == true
                        && (branchId == 0 || e.EmpUserBrDeptMappings.Any(m => m.BranchId == branchId))
                        && (departmentId == 0 || e.EmpUserBrDeptMappings.Any(m => m.DepartmentId == departmentId)));

                int totalStaffCount = await employeeQuery.CountAsync();

                var todayLogsQuery = db.AttendanceLogs.AsNoTracking()
                    .Where(a => a.EmployeeId != null && a.Employee.IsActive == true
                        && (branchId == 0 || a.Employee.EmpUserBrDeptMappings.Any(m => m.BranchId == branchId))
                        && (departmentId == 0 || a.Employee.EmpUserBrDeptMappings.Any(m => m.DepartmentId == departmentId))
                        && (
                            (a.TimeInAt != null && a.TimeInAt >= today && a.TimeInAt < tomorrow) ||
                            (a.TimeInMobile != null && a.TimeInMobile >= today && a.TimeInMobile < tomorrow) ||
                            (a.TimeInImage != null && a.TimeInImage >= today && a.TimeInImage < tomorrow)
                        ));

                var presentToday = await todayLogsQuery.Select(a => a.EmployeeId).Distinct().CountAsync();
                var checkedIn = await todayLogsQuery.CountAsync();
                var checkedOut = await todayLogsQuery
                    .Where(a => a.TimeOutAt != null || a.TimeOutMobile != null || a.TimeOutImage != null)
                    .CountAsync();


                IQueryable<AttendanceLog> attendance = db.AttendanceLogs.AsNoTracking().Include(x => x.Employee).ThenInclude(x => x.ShiftType)
                    .Where(a => a.EmployeeId != null && a.Employee.IsActive == true
                        && (branchId == 0 || a.Employee.EmpUserBrDeptMappings.Any(m => m.BranchId == branchId))
                        && (departmentId == 0 || a.Employee.EmpUserBrDeptMappings.Any(m => m.DepartmentId == departmentId))
                        && (
                            (a.TimeInAt.HasValue && a.TimeInAt >= rangeStart && a.TimeInAt < rangeEnd) ||
                            (a.TimeInMobile.HasValue && a.TimeInMobile >= rangeStart && a.TimeInMobile < rangeEnd) ||
                            (a.TimeInImage.HasValue && a.TimeInImage >= rangeStart && a.TimeInImage < rangeEnd)
                        ));

                var rawLogs = await attendance
                            .OrderByDescending(a => a.TimeInAt ?? a.TimeInMobile ?? a.TimeInImage)
                            .Include(a => a.Employee)
                                .ThenInclude(e => e.ShiftType)
                            .Include(a => a.Employee)
                                .ThenInclude(e => e.EmpUserBrDeptMappings)
                                    .ThenInclude(m => m.Branch)
                            .Include(a => a.Employee)
                                .ThenInclude(e => e.EmpUserBrDeptMappings)
                                    .ThenInclude(m => m.Department)
                            .Include(a => a.AttendanceStatus)
                            .Skip((Page - 1) * PageSize)
                            .Take(PageSize)
                            .ToListAsync();

                var records = rawLogs.Select(log =>
                {
                    var date_in = log.TimeInAt ?? log.TimeInMobile ?? log.TimeInImage;
                    var date_out = log.TimeOutAt ?? log.TimeOutMobile ?? log.TimeOutImage;

                    TimeSpan? workedDuration = (date_in.HasValue && date_out.HasValue)
                        ? date_out.Value - date_in.Value
                        : null;

                    string workedHours = workedDuration.HasValue
                        ? $"{(int)workedDuration.Value.TotalHours}h {workedDuration.Value.Minutes}m"
                        : "Incomplete";

                    double difference = workedDuration.HasValue
                        ? workedDuration.Value.TotalHours - (log.Employee.ShiftType?.ShiftHours ?? 0)
                        : 0;

                    return new
                    {
                        log.EmployeeId,
                        log.Employee.EmployeeName,
                        log.Employee.EmployeeCode,
                        BranchName = log.Employee.EmpUserBrDeptMappings
                            .Where(x => x.IsPrimaryBranch == true)
                            .Select(x => x.Branch.BranchName)
                            .FirstOrDefault(),
                        DepartmentName = log.Employee.EmpUserBrDeptMappings
                            .Where(x => x.IsPrimaryBranch == true)
                            .Select(x => x.Department.DepartmentName)
                            .FirstOrDefault(),
                        CheckInAt = date_in?.ToShortTimeString(),
                        CheckInLocation = log.TimeInLocationName,
                        CheckOutAt = date_out?.ToShortTimeString(),
                        CheckOutLocation = log.TimeOutLocationName,
                        log.Employee.ShiftType?.ShiftHours,
                        PresentedHours = workedHours,
                        Difference = difference,
                    };
                }).ToList();

                return Ok(new
                {
                    Stats = new
                    {
                        TotalEmployees = totalStaffCount,
                        PresentToday = presentToday,
                        CheckedIn = checkedIn,
                        CheckedOut = checkedOut
                    },
                    Records = records
                });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        private static string GetDate(DateTime? date_1, DateTime? date_2, DateTime? date_3)
        {
            var date = date_1 ?? date_2 ?? date_3;
            return date?.ToString("dd-MMM-yyyy");
        }
    }
}