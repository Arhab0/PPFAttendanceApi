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
        public async Task<IActionResult> GetAttendanceHistory(int month)
        {
            try
            {
                var sid = int.Parse(claims["sid"]);
                var roleId = int.Parse(claims["RoleId"]);

                IQueryable<AttendanceLog> query = db.AttendanceLogs
                    .Where(x =>
                        (x.TimeInAt.HasValue && x.TimeInAt.Value.Month == month) ||
                        (x.TimeInMobile.HasValue && x.TimeInMobile.Value.Month == month) ||
                        (x.TimeInImage.HasValue && x.TimeInImage.Value.Month == month));

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
            await db.Database.BeginTransactionAsync();
            try
            {
                var sid = int.Parse(claims["sid"]);
                var roleId = int.Parse(claims["RoleId"]);

                var checkDate = (dto.TimeInAt != null ? dto.TimeInAt : dto.TimeInMobile != null ? dto.TimeInMobile : dto.TimeInImage)?.Date
                                ?? DateTime.UtcNow.Date;

                if (roleId == 3)
                {
                    var employeeCode = (await db.Employees.AsNoTracking().Where(x => x.EmployeeId == sid).FirstAsync()).EmployeeCode;
                    var check = await db.AttendanceLogs
                        .Where(x =>
                            x.EmployeeId == sid &&
                                 (
                                     (x.TimeInAt.HasValue && x.TimeInAt.Value.Date == checkDate) ||
                                     (x.TimeInMobile.HasValue && x.TimeInMobile.Value.Date == checkDate) ||
                                     (x.TimeInImage.HasValue && x.TimeInImage.Value.Date == checkDate)
                                 )
                             )
                        .FirstOrDefaultAsync();

                    if (check != null && (check.TimeInAt != null || check.TimeInMobile != null || check.TimeInImage != null) && (check.TimeOutAt != null || check.TimeOutMobile != null || check.TimeOutImage != null))
                        return BadRequest(new { statusCode = 400, message = "Attendance already marked for today." });

                    if (check == null)
                    {
                        if (string.IsNullOrEmpty(dto.TimeInLat) || string.IsNullOrEmpty(dto.TimeInLon) || dto.AttendanceInImage == null || string.IsNullOrEmpty(dto.TimeInLocationName))
                            return BadRequest(new { statusCode = 400, message = "Missing required fields for time in." });

                        var log = new AttendanceLog
                        {
                            AttendanceInLat = dto.TimeInLat,
                            AttendanceInLon = dto.TimeInLon,
                            TimeInAt = dto.TimeInAt ?? null,
                            TimeInMobile = dto.TimeInMobile ?? null,
                            TimeInImage = dto.TimeInImage ?? null,
                            TimeInLocationName = dto.TimeInLocationName,
                            TimeInBy = "Self",
                            AttendanceStatusId = 1,
                            EmployeeId = sid,
                            TimeInType = dto.TimeInType,
                            AttendanceInImagePath = dto.AttendanceInImage != null ? await UploadDoc.UploadEmployeeAttendaceImage(dto.AttendanceInImage, employeeCode) : "no image provided"
                        };

                        await db.AttendanceLogs.AddAsync(log);
                        await db.SaveChangesAsync();
                        await db.Database.CommitTransactionAsync();
                        return Json(new { statusCode = 200, message = "Time in marked successfully." });
                    }

                    if (string.IsNullOrEmpty(dto.TimeOutLat) || string.IsNullOrEmpty(dto.TimeOutLon) || dto.AttendanceOutImage == null
                        || string.IsNullOrEmpty(dto.TimeOutLocationName))
                        return BadRequest(new { statusCode = 400, message = "Missing required fields for time out." });

                    check.AttendanceOutLat = dto.TimeOutLat;
                    check.AttendanceOutLon = dto.TimeOutLon;
                    check.TimeOutAt = dto.TimeOutAt ?? null;
                    check.TimeOutMobile = dto.TimeOutMobile ?? null;
                    check.TimeOutImage = dto.TimeOutImage ?? null;
                    check.TimeOutLocationName = dto.TimeOutLocationName;
                    check.TimeOutBy = "Self";
                    check.AttendanceStatusId = 2;
                    check.TimeOutType = dto.TimeOutType;
                    check.AttendanceOutImagePath = dto.AttendanceOutImage != null ? await UploadDoc.UploadEmployeeAttendaceImage(dto.AttendanceOutImage, employeeCode) : "no image provided";


                    await db.SaveChangesAsync();
                    await db.Database.CommitTransactionAsync();
                    return Json(new { statusCode = 200, message = "Time out marked successfully." });
                }

                var check_ = await db.AttendanceLogs
                    .Where(x =>
                        x.UserId == sid &&
                            (
                                 (x.TimeInAt.HasValue && x.TimeInAt.Value.Date == checkDate) ||
                                 (x.TimeInMobile.HasValue && x.TimeInMobile.Value.Date == checkDate) ||
                                 (x.TimeInImage.HasValue && x.TimeInImage.Value.Date == checkDate)
                            )
                         )
                    .FirstOrDefaultAsync();

                if (check_ != null && (check_.TimeInAt != null || check_.TimeInMobile != null || check_.TimeInImage != null) && (check_.TimeOutAt != null || check_.TimeOutMobile != null || check_.TimeOutImage != null))
                    return BadRequest(new { statusCode = 400, message = "Attendance already marked for today." });

                if (check_ == null)
                {
                    if (string.IsNullOrEmpty(dto.TimeInLat) || string.IsNullOrEmpty(dto.TimeInLon) || dto.AttendanceInImage == null || string.IsNullOrEmpty(dto.TimeInLocationName))
                        return BadRequest(new { statusCode = 400, message = "Missing required fields for time in." });

                    var log = new AttendanceLog
                    {
                        AttendanceInLat = dto.TimeInLat,
                        AttendanceInLon = dto.TimeInLon,
                        TimeInAt = dto.TimeInAt ?? null,
                        TimeInMobile = dto.TimeInMobile ?? null,
                        TimeInImage = dto.TimeInImage ?? null,
                        TimeInLocationName = dto.TimeInLocationName,
                        TimeInBy = "Self",
                        AttendanceStatusId = 1,
                        UserId = sid,
                        TimeInType = dto.TimeInType,
                        AttendanceInImagePath = dto.AttendanceInImage != null ? await UploadDoc.UploadStaffAttendaceImage(dto.AttendanceInImage, sid.ToString()) : "no image provided"
                    };

                    await db.AttendanceLogs.AddAsync(log);
                    await db.SaveChangesAsync();
                    await db.Database.CommitTransactionAsync();
                    return Json(new { statusCode = 200, message = "Time in marked successfully." });
                }

                if (string.IsNullOrEmpty(dto.TimeOutLat) || string.IsNullOrEmpty(dto.TimeOutLon) || dto.AttendanceOutImage == null
                    || string.IsNullOrEmpty(dto.TimeOutLocationName))
                    return BadRequest(new { statusCode = 400, message = "Missing required fields for time out." });

                check_.AttendanceOutLat = dto.TimeOutLat;
                check_.AttendanceOutLon = dto.TimeOutLon;
                check_.TimeOutAt = dto.TimeOutAt ?? null;
                check_.TimeOutMobile = dto.TimeOutMobile ?? null;
                check_.TimeOutImage = dto.TimeOutImage ?? null;
                check_.TimeOutLocationName = dto.TimeOutLocationName;
                check_.TimeOutBy = "Self";
                check_.AttendanceStatusId = 2;
                check_.TimeOutType = dto.TimeOutType;
                check_.AttendanceOutImagePath = dto.AttendanceOutImage != null ? await UploadDoc.UploadStaffAttendaceImage(dto.AttendanceOutImage, sid.ToString()) : "no image provided";


                await db.SaveChangesAsync();
                await db.Database.CommitTransactionAsync();
                return Json(new { statusCode = 200, message = "Time out marked successfully." });
            }
            catch (Exception ex)
            {
                await db.Database.RollbackTransactionAsync();
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("MarkAttendanceFromManager")]
        public async Task<IActionResult> MarkAttendanceFromManager(AttendanceDto dto)
        {
            await db.Database.BeginTransactionAsync();
            try
            {
                var roleId = int.Parse(claims["RoleId"]);

                var checkDate = (dto.TimeInAt != null ? dto.TimeInAt : dto.TimeInMobile != null ? dto.TimeInMobile : dto.TimeInImage)?.Date
                                ?? DateTime.UtcNow.Date;

                var employeeCode = (await db.Employees.Where(x => x.EmployeeId == dto.EmployeeId).FirstAsync()).EmployeeCode;

                var check = await db.AttendanceLogs
                    .Where(x =>
                        x.EmployeeId == dto.EmployeeId &&
                             (
                                 (x.TimeInAt.HasValue && x.TimeInAt.Value.Date == checkDate) ||
                                 (x.TimeInMobile.HasValue && x.TimeInMobile.Value.Date == checkDate) ||
                                 (x.TimeInImage.HasValue && x.TimeInImage.Value.Date == checkDate)
                             )
                         )
                    .FirstOrDefaultAsync();

                if (check != null && (check.TimeInAt != null || check.TimeInMobile != null || check.TimeInImage != null) && (check.TimeOutAt != null || check.TimeOutMobile != null || check.TimeOutImage != null))
                    return BadRequest(new { statusCode = 400, message = "Attendance already marked for today." });

                if (check == null)
                {
                    if (string.IsNullOrEmpty(dto.TimeInLat) || string.IsNullOrEmpty(dto.TimeInLon) || dto.AttendanceInImage == null || string.IsNullOrEmpty(dto.TimeInLocationName))
                        return BadRequest(new { statusCode = 400, message = "Missing required fields for time in." });

                    var log = new AttendanceLog
                    {
                        AttendanceInLat = dto.TimeInLat,
                        AttendanceInLon = dto.TimeInLon,
                        TimeInAt = dto.TimeInAt ?? null,
                        TimeInMobile = dto.TimeInMobile ?? null,
                        TimeInImage = dto.TimeInImage ?? null,
                        TimeInLocationName = dto.TimeInLocationName,
                        TimeInBy = "Attendance Manager",
                        AttendanceStatusId = 1,
                        EmployeeId = dto.EmployeeId,
                        TimeInType = dto.TimeInType,
                        AttendanceInImagePath = dto.AttendanceInImage != null ? await UploadDoc.UploadEmployeeAttendaceImage(dto.AttendanceInImage, employeeCode) : "no image provided"
                    };

                    await db.AttendanceLogs.AddAsync(log);
                    await db.SaveChangesAsync();
                    await db.Database.CommitTransactionAsync();
                    return Json(new { statusCode = 200, message = "Time in marked successfully." });
                }

                if (string.IsNullOrEmpty(dto.TimeOutLat) || string.IsNullOrEmpty(dto.TimeOutLon) || dto.AttendanceOutImage == null || string.IsNullOrEmpty(dto.TimeOutLocationName))
                    return BadRequest(new { statusCode = 400, message = "Missing required fields for time out." });

                check.AttendanceOutLat = dto.TimeOutLat;
                check.AttendanceOutLon = dto.TimeOutLon;
                check.TimeOutAt = dto.TimeOutAt ?? null;
                check.TimeOutMobile = dto.TimeOutMobile ?? null;
                check.TimeOutImage = dto.TimeOutImage ?? null;
                check.TimeOutLocationName = dto.TimeOutLocationName;
                check.TimeOutBy = "Attendance Manager";
                check.AttendanceStatusId = 2;
                check.TimeOutType = dto.TimeOutType;
                check.AttendanceOutImagePath = dto.AttendanceOutImage != null ? await UploadDoc.UploadEmployeeAttendaceImage(dto.AttendanceOutImage, employeeCode) : "no image provided";

                await db.SaveChangesAsync();
                await db.Database.CommitTransactionAsync();
                return Json(new { statusCode = 200, message = "Time out marked successfully." });
            }
            catch (Exception ex)
            {
                await db.Database.RollbackTransactionAsync();
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("SyncAttendance")]
        public async Task<IActionResult> SyncAttendance(List<AttendanceDto> dto)
        {
            await db.Database.BeginTransactionAsync();
            try
            {
                if (dto.Count == 0)
                    return BadRequest(new { statusCode = 400, message = "No attendance data to sync." });

                var sid = int.Parse(claims["sid"]);
                var roleId = int.Parse(claims["RoleId"]);
                var employeeData = roleId == 3
                    ? await db.Employees.AsNoTracking().Where(x => x.EmployeeId == sid).FirstOrDefaultAsync()
                    : null;

                var existingLogs = roleId == 3
                    ? await db.AttendanceLogs.Where(x => x.EmployeeId == sid).ToListAsync()
                    : await db.AttendanceLogs.Where(x => x.UserId == sid).ToListAsync();

                var existingByDate = existingLogs
                    .Where(x => x.TimeInAt.HasValue || x.TimeInMobile.HasValue || x.TimeInImage.HasValue)
                    .GroupBy(x => (x.TimeInAt ?? x.TimeInMobile ?? x.TimeInImage)!.Value.Date)
                    .ToDictionary(g => g.Key, g => g.First());

                var newEntries = new List<AttendanceDto>();
                var skipped = new List<string>();
                var updatedCount = 0;

                foreach (var item in dto)
                {
                    var itemDate = item.TimeInAt != null
                        ? item.TimeInAt
                        : item.TimeInMobile != null
                        ? item.TimeInMobile
                        : item.TimeInImage;

                    if (itemDate == null)
                    {
                        skipped.Add("Skipped one entry — TimeInAt and TimeInMobile and TimeInImage are missing.");
                        continue;
                    }

                    var date = itemDate.Value.Date;

                    if (existingByDate.TryGetValue(date, out var existingLog))
                    {
                        if (existingLog.TimeOutAt == null && existingLog.TimeOutMobile == null && existingLog.TimeOutImage == null
                            && (item.TimeOutAt != null || item.TimeOutMobile != null || item.TimeOutImage != null))
                        {
                            existingLog.AttendanceOutLat = item.TimeOutLat;
                            existingLog.AttendanceOutLon = item.TimeOutLon;
                            existingLog.TimeOutAt = item.TimeOutAt ?? null;
                            existingLog.TimeOutMobile = item.TimeOutMobile ?? null;
                            existingLog.TimeOutImage = item.TimeOutImage ?? null;
                            existingLog.TimeOutLocationName = item.TimeOutLocationName;
                            existingLog.TimeOutBy = "Self";
                            existingLog.TimeOutType = item.TimeOutType;
                            existingLog.AttendanceStatusId = 2;
                            existingLog.AttendanceOutImagePath = item.AttendanceOutImage != null ? await (employeeData != null ? UploadDoc.UploadEmployeeAttendaceImage(item.AttendanceOutImage, employeeData.EmployeeCode) : UploadDoc.UploadStaffAttendaceImage(item.AttendanceOutImage, sid.ToString())) : "No Image Provided";
                            updatedCount++;
                        }
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(item.TimeInLat) ||
                            string.IsNullOrEmpty(item.TimeInLon) ||
                            string.IsNullOrEmpty(item.TimeInLocationName) ||
                            item.AttendanceInImage == null
                            )
                        {
                            skipped.Add($"Skipped {date.ToShortDateString()} — TimeIn location fields missing.");
                            continue;
                        }
                        newEntries.Add(item);
                    }
                }

                if (newEntries.Count == 0 && updatedCount == 0)
                    return Json(new { statusCode = 200, message = "No new attendance data to sync.", skipped });

                var logs = await Task.WhenAll(newEntries.Select(async item =>
                {
                    var log = new AttendanceLog
                    {
                        AttendanceInLat = item.TimeInLat,
                        AttendanceInLon = item.TimeInLon,
                        TimeInAt = item.TimeInAt ?? null,
                        TimeInMobile = item.TimeInMobile ?? null,
                        TimeInImage = item.TimeInImage ?? null,
                        TimeInLocationName = item.TimeInLocationName,
                        TimeInBy = "Self",
                        TimeInType = item.TimeInType,
                        AttendanceInImagePath = item.AttendanceInImage != null ? await (employeeData != null ? UploadDoc.UploadEmployeeAttendaceImage(item.AttendanceInImage, employeeData.EmployeeCode) : UploadDoc.UploadStaffAttendaceImage(item.AttendanceInImage, sid.ToString())) : "no image provided",
                        AttendanceOutLat = item.TimeOutLat,
                        AttendanceOutLon = item.TimeOutLon,
                        TimeOutAt = item.TimeOutAt ?? null,
                        TimeOutMobile = item.TimeOutMobile ?? null,
                        TimeOutImage = item.TimeOutImage ?? null,
                        TimeOutLocationName = item.TimeOutLocationName,
                        TimeOutBy = (item.TimeOutAt != null || item.TimeOutMobile != null || item.TimeOutImage != null) ? "Self" : null,
                        TimeOutType = item.TimeOutType,
                        AttendanceOutImagePath = item.AttendanceOutImage != null ? await (employeeData != null ? UploadDoc.UploadEmployeeAttendaceImage(item.AttendanceOutImage, employeeData.EmployeeCode) : UploadDoc.UploadStaffAttendaceImage(item.AttendanceOutImage, sid.ToString())) : "no image provided",

                        AttendanceStatusId = (item.TimeOutAt != null || item.TimeOutMobile != null || item.TimeOutImage != null) ? 2 : 1
                    };

                    if (roleId == 3) log.EmployeeId = sid;
                    else log.UserId = sid;

                    return log;
                }).ToList());

                await db.AttendanceLogs.AddRangeAsync(logs);
                await db.SaveChangesAsync();
                await db.Database.CommitTransactionAsync();

                return Json(new
                {
                    statusCode = 200,
                    message = $"Synced {logs.Length} new and updated {updatedCount} existing records successfully.",
                    skipped
                });
            }
            catch (Exception ex)
            {
                await db.Database.RollbackTransactionAsync();
                return BadRequest(ex.Message);
            }
        }
    }
}