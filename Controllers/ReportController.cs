using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PPFAttendanceApi.Dto;
using PPFAttendanceApi.Helper;
using PPFAttendanceApi.Models;

namespace PPFAttendanceApi.Controllers
{
    [Route("api/[controller]")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class ReportController(ppfdbContext _context, ClaimsService _claims) : Controller
    {
        private readonly ppfdbContext db = _context;
        private readonly ClaimsService claims = _claims;

        [AllowAnonymous]
        [HttpGet("DetailedAttendanceReport")]
        public async Task<IActionResult> DetailedAttendanceReport(int employeeId, DateTime From, DateTime To)
        {
            try
            {
                var employee = await db.Employees
                    .AsNoTracking()
                    .Include(x => x.ShiftType)
                    .FirstOrDefaultAsync(x => x.EmployeeId == employeeId);

                if (employee == null)
                    return NotFound($"Employee {employeeId} not found.");

                if (employee.ShiftType == null)
                    return BadRequest("Employee has no shift type configured.");

                var fromDate = From.Date;
                var toDate = To.Date;
                var toDateExclusive = toDate.AddDays(1);

                var logs = await db.AttendanceLogs.AsNoTracking()
                    .Where(x => x.EmployeeId == employeeId &&
                        ((x.TimeInAt != null && x.TimeInAt >= fromDate && x.TimeInAt < toDateExclusive) ||
                         (x.TimeInMobile != null && x.TimeInMobile >= fromDate && x.TimeInMobile < toDateExclusive) ||
                         (x.TimeInImage != null && x.TimeInImage >= fromDate && x.TimeInImage < toDateExclusive)))
                    .ToListAsync();

                var byDate = logs
                    .OrderBy(x => x.TimeInAt ?? x.TimeInMobile ?? x.TimeInImage)
                    .GroupBy(x => (x.TimeInAt ?? x.TimeInMobile ?? x.TimeInImage)!.Value.Date)
                    .ToDictionary(g => g.Key, g => g.First());

                var report = new List<DetailedAttendanceReportDto>();

                for (var i = fromDate; i <= toDate; i = i.AddDays(1))
                {
                    byDate.TryGetValue(i, out var log);

                    var dto = new DetailedAttendanceReportDto
                    {
                        EmployeeName = employee.EmployeeName,
                        EmployeeCode = employee.EmployeeCode,
                        ScheduledWorkingHours = employee.ShiftType.ShiftHours,
                        AttendanceDate = DateOnly.FromDateTime(i)
                    };

                    if (log == null)
                    {
                        dto.WorkedHours = "0";
                        dto.Difference = 0;
                        dto.PresentStatus = "Absent";
                    }
                    else
                    {
                        var timeIn = log.TimeInAt ?? log.TimeInMobile ?? log.TimeInImage;
                        var timeOut = log.TimeOutAt ?? log.TimeOutMobile ?? log.TimeOutImage;
                        dto.PresentStatus = "Present";
                        TimeSpan? worked = (timeIn.HasValue && timeOut.HasValue)
                            ? timeOut.Value - timeIn.Value
                            : null;

                        dto.TimeIn = timeIn;
                        dto.TimeOut = timeOut;
                        dto.WorkedHours = worked.HasValue
                            ? $"{(int)worked.Value.TotalHours}h {worked.Value.Minutes}m"
                            : "Incomplete";
                        dto.Difference = worked.HasValue
                            ? Math.Round(worked.Value.TotalHours - employee.ShiftType.ShiftHours,2)
                            : 0;
                    }

                    report.Add(dto);
                }

                return Json(report);
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }
    }
}
