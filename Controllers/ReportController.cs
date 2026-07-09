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

        // Attendance Reports Required
        [HttpGet("DetailedAttendanceReport")]
        public async Task<IActionResult> DetailedAttendanceReport(int employeeId, DateTime From, DateTime To)
        {
            try
            {
                var employee = await db.Employees
                    .AsNoTracking()
                    .Include(x => x.ShiftType)
                    .Include(x=>x.Role)
                    .FirstOrDefaultAsync(x => x.EmployeeId == employeeId);

                if (employee == null)
                    return NotFound($"Employee {employeeId} not found.");

                if (employee.ShiftType == null)
                    return BadRequest("Employee has no shift type configured.");

                var fromDate = employee.CreatedAt.Date > From.Date
                            ? DateOnly.FromDateTime(employee.CreatedAt.Date)
                            : DateOnly.FromDateTime(From);
                var toDate = DateOnly.FromDateTime(To);
                var toDateExclusive = toDate.AddDays(1);

                var logs = await db.AttendanceLogs.AsNoTracking()
                    .Where(x => x.EmployeeId == employeeId &&
                    (x.AttendanceDate != null && x.AttendanceDate >= fromDate && x.AttendanceDate < toDateExclusive))
                    .ToListAsync();

                var byDate = logs
                    .OrderBy(x => x.AttendanceDate)
                    .GroupBy(x => x.AttendanceDate)
                    .ToDictionary(g => g.Key, g => g.First());

                var report = new List<DetailedAttendanceReportDto>();

                for (var i = fromDate; i <= toDate; i = i.AddDays(1))
                {
                    byDate.TryGetValue(i, out var log);

                    if (log == null && i.DayOfWeek == DayOfWeek.Sunday)
                    {
                        continue;
                    }
                    var dto = new DetailedAttendanceReportDto
                    {
                        EmployeeName = employee.EmployeeName,
                        EmployeeCode = employee.EmployeeCode,
                        PhoneNumber = employee.MobileNumber,
                        RoleName = employee.Role.RoleName,
                        IsActive = employee.IsActive ? "Active" : "Inactive",
                        ScheduledWorkingHours = employee.ShiftType.ShiftHours,
                        AttendanceDate = i
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
                            ? Math.Round(worked.Value.TotalHours - employee.ShiftType.ShiftHours, 2)
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

        // Employee Attendance Summary
        [HttpGet("EmployeeAttendanceSummary")]
        public async Task<IActionResult> EmployeeAttendanceSummary(int branchId = 0, int departmentId = 0, DateTime? From = null, DateTime? To = null)
        {
            try
            {
                var f = From ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                var t = To ?? f.AddMonths(1).AddDays(-1);

                DateOnly fromDate = DateOnly.FromDateTime(f);
                DateOnly toDate = DateOnly.FromDateTime(t);
                var toDateExclusive = toDate.AddDays(1);
                var totalDays = (To?.Date - From?.Date)?.Days + 1;

                if (totalDays <= 0)
                    return BadRequest("'To' date must be on or after 'From' date.");

                var empIds = await db.EmpUserBrDeptMappings
                    .Where(e =>
                        (branchId == 0 || e.BranchId == branchId) &&
                        (departmentId == 0 || e.DepartmentId == departmentId)
                    )
                    .Select(x => x.EmployeeId)
                    .Distinct()
                    .ToListAsync();

                if (empIds.Count == 0)
                    return Json(new List<EmployeeAttendanceSummaryDto>());

                var employees = await db.Employees.AsNoTracking()
                    .Include(x => x.ShiftType)
                    .Include(x=>x.Role)
                    .Where(x => empIds.Contains(x.EmployeeId))
                    .ToListAsync();

                var logs = await db.AttendanceLogs.AsNoTracking()
                .Where(x => empIds.Contains(x.EmployeeId) &&
                (x.AttendanceDate != null && x.AttendanceDate >= fromDate && x.AttendanceDate < toDateExclusive))
                .ToListAsync();

                var logsByEmployee = logs
                    .GroupBy(x => x.EmployeeId)
                    .ToDictionary(
                        g => g.Key,
                        g => g.OrderBy(x => x.AttendanceDate)
                              .GroupBy(x => x.AttendanceDate)
                              .ToDictionary(dg => dg.Key, dg => dg.First())
                    );

                var report = new List<EmployeeAttendanceSummaryDto>();

          
                foreach (var employee in employees)
                {
                   
                    var shiftHours = employee.ShiftType.ShiftHours;

                    var employeeFromDate = employee.CreatedAt.Date > From?.Date
                        ? DateOnly.FromDateTime(employee.CreatedAt.Date)
                        : fromDate;

                    if (employeeFromDate > toDate)
                    {
                        // Employee was hired after the entire requested range — nothing to report.
                        report.Add(new EmployeeAttendanceSummaryDto
                        {
                            EmployeeName = employee.EmployeeName,
                            EmployeeCode = employee.EmployeeCode,
                            PhoneNumber = employee.MobileNumber,
                            RoleName = employee.Role.RoleName,
                            IsActive = employee.IsActive ? "Active" : "Inactive",
                            TotalScheduledHours = 0,
                            TotalWorkedHours = 0,
                            TotalShortHours = 0,
                            TotalExcessHours = 0,
                            TotalPresentDays = 0,
                            TotalAbsentDays = 0
                        });
                        continue;
                    }

                    double totalWorkedHours = 0;
                    double totalShortHours = 0;
                    double totalExcessHours = 0;
                    int totalPresentDays = 0;
                    int totalAbsentDays = 0;
                    int minusDays = 0;

                    logsByEmployee.TryGetValue(employee.EmployeeId, out var employeeLogsByDate);

                    for (var day = employeeFromDate; day <= toDate; day = day.AddDays(1))
                    {
                        AttendanceLog log = null;
                        employeeLogsByDate?.TryGetValue(day, out log);

                        if (log == null)
                        {
                            if (day.DayOfWeek == DayOfWeek.Sunday)
                            {
                                minusDays++;
                                continue;
                            }
                            totalAbsentDays++;
                            continue;
                        }

                        var timeIn = log.TimeInAt ?? log.TimeInMobile ?? log.TimeInImage;
                        var timeOut = log.TimeOutAt ?? log.TimeOutMobile ?? log.TimeOutImage;

                        if (!timeIn.HasValue)
                        {
                            totalAbsentDays++;
                            continue;
                        }

                        totalPresentDays++;

                        if (timeOut.HasValue)
                        {
                            var workedHours = (timeOut.Value - timeIn.Value).TotalHours;
                            totalWorkedHours += workedHours;

                            var diff = workedHours - shiftHours;
                            if (diff < 0)
                                totalShortHours += Math.Abs(diff);
                            else
                                totalExcessHours += diff;
                        }
                    }
                    
                    var totalScheduledHours = shiftHours * totalPresentDays;

                    report.Add(new EmployeeAttendanceSummaryDto
                    {
                        EmployeeName = employee.EmployeeName,
                        EmployeeCode = employee.EmployeeCode,
                        PhoneNumber = employee.MobileNumber,
                        IsActive = employee.IsActive ? "Active" : "Inactive",
                        TotalScheduledHours = totalScheduledHours,
                        TotalWorkedHours = Math.Round(totalWorkedHours, 2),
                        TotalShortHours = Math.Round(totalShortHours, 2),
                        TotalExcessHours = Math.Round(totalExcessHours, 2),
                        TotalPresentDays = totalPresentDays,
                        TotalAbsentDays = totalAbsentDays
                    });
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
