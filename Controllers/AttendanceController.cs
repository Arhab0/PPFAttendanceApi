using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PPFAttendanceApi.Helper;
using PPFAttendanceApi.Models;
using PPFAttendanceApi.Dto;

namespace PPFAttendanceApi.Controllers
{
    [Route("api/[controller]")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class AttendanceController(ppfdbContext _context, ClaimsService _claims) : Controller
    {
        private readonly ppfdbContext db = _context;
        private readonly ClaimsService claims = _claims;

        [HttpGet("GetAttendanceHistory")]
        public async Task<IActionResult> GetAttendanceHistory(string from, string to)
        {
            try
            {
                var sid = int.Parse(claims["sid"]);
                var roleId = int.Parse(claims["RoleId"]);

                var startDate = DateTime.SpecifyKind(DateTime.Parse(from).Date, DateTimeKind.Unspecified);
                var endDate = DateTime.SpecifyKind(DateTime.Parse(to).Date.AddDays(1), DateTimeKind.Unspecified);

                IQueryable<AttendanceLog> query = db.AttendanceLogs
                    .Where(x =>
                        (x.TimeInAt.HasValue && x.TimeInAt.Value >= startDate && x.TimeInAt.Value < endDate) ||
                        (x.TimeInMobile.HasValue && x.TimeInMobile.Value >= startDate && x.TimeInMobile.Value < endDate) ||
                        (x.TimeInImage.HasValue && x.TimeInImage.Value >= startDate && x.TimeInImage.Value < endDate));

                query = roleId == 3
                    ? query.Where(x => x.EmployeeId == sid)
                    : query.Where(x => x.UserId == sid);

                var logs = await query
                    .OrderBy(x => x.TimeInAt ?? x.TimeInMobile ?? x.TimeInImage)
                    .ToListAsync();

                var records = logs.Select(log =>
                {
                    var effectiveTimeIn = log.TimeInAt ?? log.TimeInMobile ?? log.TimeInImage;
                    var effectiveTimeOut = log.TimeOutAt ?? log.TimeOutMobile ?? log.TimeInImage;

                    return new
                    {
                        log.AttendanceLogId,
                        EffectiveTimeIn = effectiveTimeIn?.Date,
                        TimeInAt = log.TimeInAt?.ToString("HH:mm:ss"),
                        TimeInMobile = log.TimeInMobile?.ToString("HH:mm:ss"),
                        TimeInImage = log.TimeInImage?.ToString("HH:mm:ss"),
                        log.TimeInLocationName,
                        log.AttendanceInLat,
                        log.AttendanceInLon,
                        EffectiveTimeOut = effectiveTimeOut?.Date,
                        TimeOutAt = log.TimeOutAt?.ToString("HH:mm:ss"),
                        TimeOutMobile = log.TimeOutMobile?.ToString("HH:mm:ss"),
                        TimeOutImage = log.TimeOutImage?.ToString("HH:mm:ss"),
                        log.TimeOutLocationName,
                        log.AttendanceOutLat,
                        log.AttendanceOutLon,
                    };
                }).ToList();

                return Json(new { statusCode = 200, data = records });
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        [HttpGet("GetTodayAttendance")]
        public async Task<IActionResult> GetTodayAttendance(DateOnly date)
        {
            try
            {
                var roleId = int.Parse(claims["RoleId"]);
                var sid = int.Parse(claims["sid"]);
                var targetDate = date.ToDateTime(TimeOnly.MinValue);

                AttendanceLog? entity = roleId == 3
                    ? await db.AttendanceLogs.Include(e => e.Employee)
                        .Where(x =>
                            x.EmployeeId == sid &&
                                 (
                                     (x.TimeInAt.HasValue && x.TimeInAt.Value.Date == targetDate) ||
                                     (x.TimeInMobile.HasValue && x.TimeInMobile.Value.Date == targetDate) ||
                                     (x.TimeInImage.HasValue && x.TimeInImage.Value.Date == targetDate)
                                 )
                             )
                        .FirstOrDefaultAsync()
                    : await db.AttendanceLogs
                        .Where(x =>
                            x.UserId == sid &&
                                 (
                                     (x.TimeInAt.HasValue && x.TimeInAt.Value.Date == targetDate) ||
                                     (x.TimeInMobile.HasValue && x.TimeInMobile.Value.Date == targetDate) ||
                                     (x.TimeInImage.HasValue && x.TimeInImage.Value.Date == targetDate)
                                 )
                             )
                        .FirstOrDefaultAsync();

                var data = entity == null ? null : new
                {
                    entity.AttendanceLogId,
                    TimeInAt = entity.TimeInAt?.ToString("HH:mm:ss"),
                    TimeInMobile = entity.TimeInMobile?.ToString("HH:mm:ss"),
                    TimeInImage = entity.TimeInImage?.ToString("HH:mm:ss"),
                    AttendanceInImagePath = roleId == 3
                                                    ?
                                                    entity.AttendanceInImagePath != null ?
                                                        $"/images/employeeAttendance/{entity.Employee.EmployeeCode}/{(entity.TimeInAt ?? entity.TimeInMobile ?? entity.TimeInImage)?.ToString("dd-MMM-yyyy")}/{entity.AttendanceInImagePath}" : null
                                                    :
                                                    entity.AttendanceInImagePath != null ?
                                                     $"/images/staffAttendance/{entity.UserId.ToString()}/{(entity.TimeInAt ?? entity.TimeInMobile ?? entity.TimeInImage)?.ToString("dd-MMM-yyyy")}/{entity.AttendanceInImagePath}" : null,
                    entity.TimeInLocationName,
                    entity.AttendanceInLat,
                    entity.AttendanceInLon,
                    TimeOutAt = entity.TimeOutAt?.ToString("HH:mm:ss"),
                    TimeOutMobile = entity.TimeOutMobile?.ToString("HH:mm:ss"),
                    TimeOutImage = entity.TimeOutImage?.ToString("HH:mm:ss"),
                    entity.TimeOutLocationName,
                    entity.AttendanceOutLat,
                    entity.AttendanceOutLon,
                    AttendanceOutImagePath = roleId == 3
                                                    ?
                                                    entity.AttendanceOutImagePath != null ?
                                                        $"/images/employeeAttendance/{entity.Employee.EmployeeCode}/{(entity.TimeOutAt ?? entity.TimeOutMobile ?? entity.TimeOutImage)?.ToString("dd-MMM-yyyy")}/{entity.AttendanceOutImagePath}" : null
                                                    :
                                                    entity.AttendanceOutImagePath != null ?
                                                     $"/images/staffAttendance/{entity.UserId.ToString()}/{(entity.TimeOutAt ?? entity.TimeOutMobile ?? entity.TimeOutImage)?.ToString("dd-MMM-yyyy")}/{entity.AttendanceOutImagePath}" : null,
                };

                return Json(new { statusCode = 200, data });
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        [HttpPost("MarkAttendance")]
        public async Task<IActionResult> MarkAttendance(AttendanceDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Action) ||
                (dto.Action != "TimeIn" && dto.Action != "TimeOut"))
            {
                return BadRequest(new { statusCode = 400, message = "Action must be 'TimeIn' or 'TimeOut'." });
            }

            await db.Database.BeginTransactionAsync();
            try
            {
                var sid = int.Parse(claims["sid"]);
                var roleId = int.Parse(claims["RoleId"]);
                bool empFlag = roleId == 3;

                string employeeCode = "";
                if (empFlag)
                {
                    var employee = await db.Employees.AsNoTracking().Where(x => x.EmployeeId == sid).FirstOrDefaultAsync();
                    if (employee == null)
                        return BadRequest(new { statusCode = 400, message = "Employee not found." });

                    employeeCode = employee.EmployeeCode;
                }

                var openSession = await db.AttendanceLogs
                    .Where(x =>
                        (empFlag ? x.EmployeeId == sid : x.UserId == sid) &&
                        x.TimeOutAt == null && x.TimeOutMobile == null && x.TimeOutImage == null)
                    .OrderByDescending(x => x.TimeInAt ?? x.TimeInMobile ?? x.TimeInImage)
                    .FirstOrDefaultAsync();

                if (dto.Action == "TimeIn")
                {
                    if (openSession != null)
                    {
                        return BadRequest(new
                        {
                            statusCode = 400,
                            message = $"Already TimeIn for {(openSession.TimeInAt ?? openSession.TimeInMobile ?? openSession.TimeInImage):yyyy-MM-dd}."
                        });
                    }

                    if (string.IsNullOrEmpty(dto.TimeInLat) || string.IsNullOrEmpty(dto.TimeInLon) ||
                        dto.AttendanceInImage == null || string.IsNullOrEmpty(dto.TimeInLocationName))
                        return BadRequest(new { statusCode = 400, message = "Missing required fields for time in." });

                    if (dto.TimeInAt == null && dto.TimeInMobile == null && dto.TimeInImage == null)
                        return BadRequest(new { statusCode = 400, message = "At least one time-in timestamp source is required." });

                    var attendance_date = dto.TimeInAt ?? dto.TimeInMobile ?? dto.TimeInImage;
                    DateOnly formattedDate = attendance_date.HasValue
                                        ? DateOnly.FromDateTime(attendance_date.Value)
                                        : new DateOnly();
                    var log = new AttendanceLog
                    {
                        AttendanceDate = formattedDate,
                        AttendanceInLat = dto.TimeInLat,
                        AttendanceInLon = dto.TimeInLon,
                        TimeInAt = dto.TimeInAt,
                        TimeInMobile = dto.TimeInMobile,
                        TimeInImage = dto.TimeInImage,
                        TimeInLocationName = dto.TimeInLocationName,
                        TimeInBy = "Self",
                        AttendanceStatusId = 1,
                        EmployeeId = empFlag ? sid : null,
                        UserId = empFlag ? null : sid,
                        TimeInType = dto.TimeInType,
                        AttendanceInImagePath = dto.AttendanceInImage != null
                            ? (empFlag
                                ? await UploadDoc.UploadEmployeeAttendaceImage(dto.AttendanceInImage, employeeCode)
                                : await UploadDoc.UploadStaffAttendaceImage(dto.AttendanceInImage, sid.ToString()))
                            : "no image provided"
                    };

                    await db.AttendanceLogs.AddAsync(log);
                    await db.SaveChangesAsync();
                    await db.Database.CommitTransactionAsync();
                    return Json(new { statusCode = 200, message = "Time in marked successfully." });
                }
                else // dto.Action == "TimeOut"
                {
                    if (openSession == null)
                    {
                        return BadRequest(new { statusCode = 400, message = "No open session found to check out from." });
                    }

                    if (string.IsNullOrEmpty(dto.TimeOutLat) || string.IsNullOrEmpty(dto.TimeOutLon) ||
                        dto.AttendanceOutImage == null || string.IsNullOrEmpty(dto.TimeOutLocationName))
                        return BadRequest(new { statusCode = 400, message = "Missing required fields for time out." });

                    if (dto.TimeOutAt == null && dto.TimeOutMobile == null && dto.TimeOutImage == null)
                        return BadRequest(new { statusCode = 400, message = "At least one time-out timestamp source is required." });

                    openSession.AttendanceOutLat = dto.TimeOutLat;
                    openSession.AttendanceOutLon = dto.TimeOutLon;
                    openSession.TimeOutAt = dto.TimeOutAt;
                    openSession.TimeOutMobile = dto.TimeOutMobile;
                    openSession.TimeOutImage = dto.TimeOutImage;
                    openSession.TimeOutLocationName = dto.TimeOutLocationName;
                    openSession.TimeOutBy = "Self";
                    openSession.AttendanceStatusId = 2;
                    openSession.TimeOutType = dto.TimeOutType;
                    openSession.AttendanceOutImagePath = dto.AttendanceOutImage != null
                        ? (empFlag
                            ? await UploadDoc.UploadEmployeeAttendaceImage(dto.AttendanceOutImage, employeeCode)
                            : await UploadDoc.UploadStaffAttendaceImage(dto.AttendanceOutImage, sid.ToString()))
                        : "no image provided";

                    await db.SaveChangesAsync();
                    await db.Database.CommitTransactionAsync();
                    return Json(new { statusCode = 200, message = "Time out marked successfully." });
                }
            }
            catch (Exception ex)
            {
                await db.Database.RollbackTransactionAsync();
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("MarkAttendanceTablet")]
        public async Task<IActionResult> MarkAttendanceTablet(AttendanceDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Action) ||
                (dto.Action != "TimeIn" && dto.Action != "TimeOut"))
            {
                return BadRequest(new { statusCode = 400, message = "Action must be 'TimeIn' or 'TimeOut'." });
            }

            await db.Database.BeginTransactionAsync();
            try
            {
                var roleId = int.Parse(claims["RoleId"]);
                bool empFlag = false;
                string employeeCode = "";

                var empCheck = await db.Employees.Where(x => x.EmployeeId == dto.sid).FirstOrDefaultAsync();

                if (empCheck == null)
                {
                    var staffCheck = await db.Users.Where(x => x.UserId == dto.sid).FirstOrDefaultAsync();

                    if (staffCheck == null)
                    {
                        return BadRequest(new { statusCode = 400, message = "No employee or user found with the given ID." });
                    }

                    if (staffCheck.IsActive == false)
                    {
                        return BadRequest(new { statusCode = 400, message = "Unable to mark attendance. User is deactivated." });
                    }
                }
                else
                {
                    if (empCheck.IsActive == false)
                    {
                        return BadRequest(new { statusCode = 400, message = "Unable to mark attendance. Employee is deactivated." });
                    }

                    employeeCode = empCheck.EmployeeCode;
                    empFlag = true;
                }

                var openSession = await db.AttendanceLogs
                    .Where(x =>
                        (empFlag ? x.EmployeeId == dto.sid : x.UserId == dto.sid) &&
                        x.TimeOutAt == null && x.TimeOutMobile == null && x.TimeOutImage == null)
                    .OrderByDescending(x => x.TimeInAt ?? x.TimeInMobile ?? x.TimeInImage)
                    .FirstOrDefaultAsync();

                if (dto.Action == "TimeIn")
                {
                    if (openSession != null)
                    {
                        return BadRequest(new
                        {
                            statusCode = 400,
                            message = $"Already TimeIn for {(openSession.TimeInAt ?? openSession.TimeInMobile ?? openSession.TimeInImage):yyyy-MM-dd}."
                        });
                    }

                    if (string.IsNullOrEmpty(dto.TimeInLat) || string.IsNullOrEmpty(dto.TimeInLon) ||
                        dto.AttendanceInImage == null || string.IsNullOrEmpty(dto.TimeInLocationName))
                        return BadRequest(new { statusCode = 400, message = "Missing required fields for time in." });

                    if (dto.TimeInAt == null && dto.TimeInMobile == null && dto.TimeInImage == null)
                        return BadRequest(new { statusCode = 400, message = "At least one time-in timestamp source is required." });

                    var attendance_date = dto.TimeInAt ?? dto.TimeInMobile ?? dto.TimeInImage;
                    DateOnly formattedDate = attendance_date.HasValue
                                        ? DateOnly.FromDateTime(attendance_date.Value)
                                        : new DateOnly();
                    var log = new AttendanceLog
                    {
                        AttendanceDate = formattedDate,
                        AttendanceInLat = dto.TimeInLat,
                        AttendanceInLon = dto.TimeInLon,
                        TimeInAt = dto.TimeInAt,
                        TimeInMobile = dto.TimeInMobile,
                        TimeInImage = dto.TimeInImage,
                        TimeInLocationName = dto.TimeInLocationName,
                        TimeInBy = "Attendance Manager",
                        AttendanceStatusId = 1,
                        EmployeeId = empFlag ? dto.sid : null,
                        UserId = empFlag ? null : dto.sid,
                        TimeInType = dto.TimeInType,
                        AttendanceInImagePath = dto.AttendanceInImage != null
                            ? await UploadDoc.UploadEmployeeAttendaceImage(dto.AttendanceInImage, employeeCode)
                            : "no image provided"
                    };

                    await db.AttendanceLogs.AddAsync(log);
                    await db.SaveChangesAsync();
                    await db.Database.CommitTransactionAsync();
                    return Json(new { statusCode = 200, message = "Time in marked successfully." });
                }
                else // dto.Action == "TimeOut"
                {
                    if (openSession == null)
                    {
                        return BadRequest(new { statusCode = 400, message = "No open session found to check out from." });
                    }

                    if (string.IsNullOrEmpty(dto.TimeOutLat) || string.IsNullOrEmpty(dto.TimeOutLon) ||
                        dto.AttendanceOutImage == null || string.IsNullOrEmpty(dto.TimeOutLocationName))
                        return BadRequest(new { statusCode = 400, message = "Missing required fields for time out." });

                    if (dto.TimeOutAt == null && dto.TimeOutMobile == null && dto.TimeOutImage == null)
                        return BadRequest(new { statusCode = 400, message = "At least one time-out timestamp source is required." });

                    openSession.AttendanceOutLat = dto.TimeOutLat;
                    openSession.AttendanceOutLon = dto.TimeOutLon;
                    openSession.TimeOutAt = dto.TimeOutAt;
                    openSession.TimeOutMobile = dto.TimeOutMobile;
                    openSession.TimeOutImage = dto.TimeOutImage;
                    openSession.TimeOutLocationName = dto.TimeOutLocationName;
                    openSession.TimeOutBy = "Attendance Manager";
                    openSession.AttendanceStatusId = 2;
                    openSession.TimeOutType = dto.TimeOutType;
                    openSession.AttendanceOutImagePath = dto.AttendanceOutImage != null
                        ? await UploadDoc.UploadEmployeeAttendaceImage(dto.AttendanceOutImage, employeeCode)
                        : "no image provided";

                    await db.SaveChangesAsync();
                    await db.Database.CommitTransactionAsync();
                    return Json(new { statusCode = 200, message = "Time out marked successfully." });
                }
            }
            catch (Exception ex)
            {
                await db.Database.RollbackTransactionAsync();
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("UpdateAttendance")]
        public async Task<IActionResult> UpdateAttendance(AttendanceDto dto)
        {
            try
            {
                var roleId = int.Parse(claims["RoleId"]);
                if (roleId != 4)
                {
                    return BadRequest(new {statusCode = 400, message = "Only HR can update attendance." });
                }
                var log = await db.AttendanceLogs.FindAsync(dto.AttendanceLogId);
                if (log == null)
                {
                    return NotFound(new { statusCode = 404, message = "Attendance log not found." });
                }
                

                var dto_time_in = dto.TimeInAt ?? dto.TimeInMobile ?? dto.TimeInImage;
                var dto_time_out = dto.TimeOutAt ?? dto.TimeOutMobile ?? dto.TimeOutImage;

                log.TimeInAt = dto.TimeInAt ?? log.TimeInAt;
                log.TimeInMobile = dto.TimeInMobile ?? log.TimeInMobile;
                log.TimeInImage = dto.TimeInImage ?? log.TimeInImage;
                log.TimeInBy = dto_time_in.HasValue ? "HR" : log.TimeInBy;

                if(!log.TimeOutAt.HasValue || !log.TimeOutMobile.HasValue || !log.TimeOutImage.HasValue)
                {
                    log.TimeOutAt = dto.TimeOutAt;
                    log.TimeOutMobile = dto.TimeOutMobile;
                    log.TimeOutImage = dto.TimeOutImage;
                    log.TimeOutBy = "HR";
                    log.AttendanceStatusId = 2;
                }
                else
                {
                    log.TimeOutAt = dto.TimeOutAt ?? log.TimeOutAt;
                    log.TimeOutMobile = dto.TimeOutMobile ?? log.TimeOutMobile;
                    log.TimeOutImage = dto.TimeOutImage ?? log.TimeOutImage;
                    log.TimeOutBy = dto_time_out.HasValue ? "HR" : log.TimeOutBy;
                }

                log.AttendanceDate = dto.AttendanceDate ?? log.AttendanceDate;

                await db.SaveChangesAsync();
                return Json(new { statusCode = 200, message = "Attendance updated successfully." });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}