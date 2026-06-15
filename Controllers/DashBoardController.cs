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
        public async Task<IActionResult> GetDashboardData(string filter = "today", DateTime? from = null, DateTime? to = null)
        {
            try
            {
                var roleId = int.Parse(claims["RoleId"]);
                var sid = int.Parse(claims["sid"]);

                int? managerId = roleId == 2 && sid > 0 ? sid : null;


                var today = DateTime.Now.Date;
                var tomorrow = today.AddDays(1);

                var employeeQuery = db.Employees.AsNoTracking().Where(e => e.IsActive == true);

                int totalStaffCount = await employeeQuery.CountAsync();


                var todayLogsQuery = db.AttendanceLogs.AsNoTracking()
                    .Where(a => (a.TimeInAt != null && a.TimeInAt >= today && a.TimeInAt < tomorrow) || ((a.TimeInMobile != null && a.TimeInMobile >= today && a.TimeInMobile < tomorrow)) || ((a.TimeInImage != null && a.TimeInImage >= today && a.TimeInImage < tomorrow)));

                todayLogsQuery = todayLogsQuery.Where(a =>
                    (a.EmployeeId != null && a.Employee.IsActive == true)
                );
                
                var presentToday = await todayLogsQuery.Select(a => a.EmployeeId).Distinct().CountAsync();
                var checkedIn = await todayLogsQuery.CountAsync();
                var checkedOut = await todayLogsQuery.Where(a => (a.TimeOutAt != null) || (a.TimeOutMobile != null) || (a.TimeOutImage != null)).CountAsync();

                DateTime rangeStart, rangeEnd;

                switch (filter.ToLower().Trim())
                {
                    case "yesterday":
                        rangeStart = today.AddDays(-1);
                        rangeEnd = today;
                        break;

                    case "last7days":
                        rangeStart = today.AddDays(-6);
                        rangeEnd = tomorrow;
                        break;

                    case "thismonth":
                        rangeStart = new DateTime(today.Year, today.Month, 1);
                        rangeEnd = tomorrow;
                        break;

                    case "custom":
                        if (from == null || to == null)
                            return BadRequest("Both 'from' and 'to' are required for custom filter.");

                        rangeStart = from.Value.Date;
                        rangeEnd = to.Value.Date.AddDays(1);

                        if (rangeStart > rangeEnd)
                            return BadRequest("'from' date cannot be after 'to' date.");
                        break;

                    default:
                        rangeStart = today;
                        rangeEnd = tomorrow;
                        break;
                }

                var recordsQuery = db.AttendanceLogs
                    .AsNoTracking()
                    .Where(a => (a.EmployeeId != null && a.Employee.IsActive == true) &&
                        (a.TimeInAt.HasValue && (a.TimeInAt >= rangeStart && a.TimeInAt < rangeEnd)) ||
                        (a.TimeInMobile.HasValue && (a.TimeInMobile >= rangeStart && a.TimeInMobile < rangeEnd)) ||
                        (a.TimeInImage.HasValue && (a.TimeInImage >= rangeStart && a.TimeInImage < rangeEnd))
                    );

                var records = await recordsQuery
                    .OrderByDescending(a => a.TimeInAt ?? a.TimeInMobile ?? a.TimeInImage)
                    .Select(a => new
                    {
                        a.EmployeeId,
                        a.Employee.EmployeeName,
                        a.Employee.EmployeeEmail,
                        ProfileImage = a.Employee.EmployeeFiles.Select(f => $"/images/employee/{a.Employee.EmployeeCode}/{f.FilePath}" ).FirstOrDefault(),
                        CheckIn = a.TimeInAt ?? a.TimeInMobile ?? a.TimeInImage,
                        CheckOut = a.TimeOutAt ?? a.TimeOutMobile ?? a.TimeOutImage,
                        LocationIn = a.AttendanceInLat != null && a.AttendanceInLon != null
                            ? new
                            {
                                Lat = a.AttendanceInLat,
                                Lon = a.AttendanceInLon,
                                Type = a.TimeInLocationName
                            }
                            : null,
                        LocationOut = a.AttendanceOutLat != null && a.AttendanceOutLon != null
                            ? new
                            {
                                Lat = a.AttendanceOutLat,
                                Lon = a.AttendanceOutLon,
                                Type = a.TimeOutLocationName
                            }
                            : null,
                        Status = a.AttendanceStatus != null ? a.AttendanceStatus.Status : null,
                        StatusId = a.AttendanceStatusId,
                        AttendanceInImagePath = a.AttendanceInImagePath != null ?
                                                        $"/images/employeeAttendance/{a.Employee.EmployeeCode}/{GetDate(a.TimeInAt,a.TimeInMobile,a.TimeInImage)}/{a.AttendanceInImagePath}" : null,

                        AttendanceOutImagePath = a.AttendanceOutImagePath != null ?
                                                        $"/images/employeeAttendance/{a.Employee.EmployeeCode}/{GetDate(a.TimeOutAt, a.TimeOutMobile, a.TimeOutImage)}/{a.AttendanceOutImagePath}" : null,
                    })
                    .ToListAsync();

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