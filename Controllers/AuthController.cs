using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PPFAttendanceApi.Dto;
using PPFAttendanceApi.Helper;
using PPFAttendanceApi.Models;
using System.ComponentModel;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace PPFAttendanceApi.Controllers
{
    [Route("api/[controller]")]
    public class AuthController(ppfdbContext _context, IConfiguration _configuration, ClaimsService _claims, IWebHostEnvironment _env) : Controller
    {
        private readonly ppfdbContext db = _context;
        private readonly IConfiguration configuration = _configuration;
        private readonly ClaimsService claims = _claims;
        private readonly IWebHostEnvironment env = _env;

        [HttpPost("Login")]
        public async Task<IActionResult> Login(LoginRequestDto dto)
        {
            try
            {
                string encrypt = Security.Encrypt(dto.Password);

                UserLoginDto obj = new();

                // ==================================== Portal
                if (dto.IsFromPortal)
                {
                    var data_1 = await db.Users.Include(x => x.Role).Include(x => x.UserFiles)
                        .Where(x => x.UserEmail.ToLower() == dto.Email.ToLower() && x.UserPassword == encrypt).FirstOrDefaultAsync();

                    var data_2 = await db.Employees.Include(x => x.Role).Include(x => x.EmployeeFiles)
                        .Where(x => x.EmployeeEmail.ToLower() == dto.Email.ToLower() && x.EmployeePassword == encrypt).FirstOrDefaultAsync();


                    if (data_1 == null && data_2 == null)
                    {
                        return NotFound(new { statusCode = 404, message = "User not found. Invalid email or password" });
                    }

                    if (data_1 != null)
                    {
                        if (data_1.IsActive == false)
                        {
                            return BadRequest(new { statusCode = 400, message = "User is deactivated" });
                        }
                        if (!data_1.Role.CanAccessPortal)
                        {
                            return Unauthorized(new { statusCode = 401, message = "Unauthorized access." });
                        }

                        obj.Id = data_1.UserId;
                        obj.Name = data_1.UserName;
                        obj.Email = data_1.UserEmail;
                        obj.RoleId = data_1.RoleId;
                        obj.RoleName = data_1.Role.RoleName;

                        var uFile_1 = data_1.UserFiles?.FirstOrDefault();
                        if (data_1.RoleId == 2)
                        {
                            obj.File = uFile_1 == null ? null : new()
                            {
                                FileId = uFile_1.UserFileId,
                                FilePath = $"/images/staff/{data_1.UserId.ToString()}/{uFile_1.FilePath}",
                                Extension = uFile_1.Extension,
                                Sid = data_1.UserId
                            };
                        }

                        var menu_1 = await db.RoleMenuMappings
                                   .AsNoTracking()
                                   .Include(x => x.Menu)
                                   .Where(x => x.RoleId == data_1.RoleId && (x.View || x.Create || x.Edit || x.Delete))
                                   .Select(x => new
                                   {
                                       x.Menu.MenuId,
                                       x.Menu.MenuName,
                                       x.Menu.SortingOrder,
                                       x.Menu.Path,
                                       x.Menu.Description,
                                       x.Menu.Icon,
                                       x.Menu.IsParent,
                                       x.Menu.ParentId,
                                       x.Menu.IsActive,
                                       x.View,
                                       x.Edit,
                                       x.Create,
                                       x.Delete
                                   })
                                   .OrderBy(x => x.SortingOrder)
                                   .ToListAsync();

                        return Json(new { obj, token = GenerateJwtToken(data_1.UserId, data_1.RoleId), menus = menu_1 });
                    }

                    if (!data_2.Role.CanAccessPortal)
                    {
                        return Unauthorized(new { statusCode = 401, message = "Unauthorized access." });
                    }
                    if (data_2.IsActive == false)
                    {
                        return BadRequest(new { statusCode = 400, message = "User is deactivated" });
                    }
                    obj.Id = data_2.EmployeeId;
                    obj.Name = data_2.EmployeeName;
                    obj.Email = data_2.EmployeeEmail;
                    obj.RoleId = data_2.RoleId;
                    obj.RoleName = data_2.Role.RoleName;

                    var uFile = data_2.EmployeeFiles?.FirstOrDefault();
                    if (data_2.RoleId == 2)
                    {
                        obj.File = uFile == null ? null : new()
                        {
                            FileId = uFile.EmployeeFileId,
                            FilePath = $"/images/employee/{data_2.EmployeeCode}/{uFile.FilePath}",
                            Extension = uFile.Extension,
                            Sid = data_2.EmployeeId
                        };
                    }

                    var menu_2 = await db.RoleMenuMappings
                                   .AsNoTracking()
                                   .Include(x => x.Menu)
                                   .Where(x => x.RoleId == data_2.RoleId && (x.View || x.Create || x.Edit || x.Delete))
                                   .Select(x => new
                                   {
                                       x.Menu.MenuId,
                                       x.Menu.MenuName,
                                       x.Menu.SortingOrder,
                                       x.Menu.Path,
                                       x.Menu.Description,
                                       x.Menu.Icon,
                                       x.Menu.IsParent,
                                       x.Menu.ParentId,
                                       x.Menu.IsActive,
                                       x.View,
                                       x.Edit,
                                       x.Create,
                                       x.Delete
                                   })
                                   .OrderBy(x => x.SortingOrder)
                                   .ToListAsync();

                    return Json(new { obj, token = GenerateJwtToken(data_2.EmployeeId, data_2.RoleId), menus = menu_2 });
                }


                // ==================================== Mobile
                var e = await db.Employees
                    .Include(x => x.Role)
                    .Include(x => x.EmployeeFiles)
                    .Include(x => x.EmpUserBrDeptMappings)
                    .Where(x => x.EmployeeEmail.ToLower() == dto.Email.ToLower() && x.EmployeePassword == encrypt).FirstOrDefaultAsync();

                var m = await db.Users
                    .Include(x => x.Role)
                    .Include(x => x.EmpUserBrDeptMappings)
                    .Include(x => x.UserFiles)
                    .Where(x => x.UserEmail.ToLower() == dto.Email.ToLower() && x.UserPassword == encrypt).FirstOrDefaultAsync();

                if (e == null && m == null)
                {
                    return NotFound(new { statusCode = 404, message = "User/Employee not found. Invalid email or password" });
                }

                if (e != null)
                {
                    if (e.IsActive == false)
                    {
                        return BadRequest(new { statusCode = 400, message = "User is deactivated" });
                    }
                    if (!e.Role.CanAccessMobileApplication)
                    {
                        return Unauthorized(new { statusCode = 401, message = "Unauthorized access." });
                    }
                    if (e.RoleId == 4)
                    {
                        //var deviceCheck = await db.DeviceSessions.Where(x => e.EmployeeId == x.EmployeeId).FirstOrDefaultAsync();

                        //if (deviceCheck == null)
                        //{
                        //    await db.DeviceSessions.AddAsync(new()
                        //    {
                        //        DeviceId = dto.DeviceId,
                        //        DeviceName = dto.DeviceName,
                        //        DevicePlatform = dto.DevicePlatform,
                        //        EmployeeId = e.EmployeeId,
                        //        CreatedAt = DateTime.Now
                        //    });

                        //    await db.SaveChangesAsync();
                        //}
                        //else if (deviceCheck.DeviceId != dto.DeviceId)
                        //{
                        //    return Unauthorized(new { statusCode = 401, message = "Unauthorized access. Employee is already logged in from another device." });
                        //}

                        obj.Id = e.EmployeeId;
                        obj.Name = e.EmployeeName;
                        obj.Email = e.EmployeeEmail;
                        obj.RoleId = e.RoleId;
                        obj.RoleName = e.Role.RoleName;

                        obj.Employees = db.Employees
                            .AsNoTracking()
                            .Include(x => x.Role)
                            .Include(x => x.Locations)
                            .Where(e => e.IsActive == true && e.RoleId == 3).Select(x => new RegisteredEmployeesData()
                            {

                                EmployeeId = x.EmployeeId,
                                EmployeeName = x.EmployeeName,
                                RoleName = x.Role.RoleName,
                                EmployeeEmail = x.EmployeeEmail,
                                EmployeeCode = x.EmployeeCode,
                                MobileNumber = x.MobileNumber,
                                File = x.EmployeeFiles
                                .Select(f => new FileDto
                                {
                                    FileId = f.EmployeeFileId,
                                    FilePath = $"/images/employee/{x.EmployeeCode}/{f.FilePath}",
                                    Extension = f.Extension,
                                    Sid = x.EmployeeId,
                                    ActiveStatus = x.IsActive,
                                }).FirstOrDefault(),

                                Locations_ = x.Locations.Select(l => new UserLocationDto()
                                {
                                    LocationId = l.LocationId,
                                    Latitude = l.Latitude,
                                    Longitude = l.Longitude,
                                    LocationType = l.LocationName,
                                    Radius = l.Radius
                                }).ToList()
                            }).ToList();

                        return Json(new { obj, token = GenerateJwtToken(e.EmployeeId, e.RoleId) });
                    }
                }

                else
                {
                    if (m.IsActive == false)
                    {
                        return BadRequest(new { statusCode = 400, message = "User is deactivated" });
                    }
                    if (!m.Role.CanAccessMobileApplication)
                    {
                        return Unauthorized(new { statusCode = 401, message = "Unauthorized access." });
                    }

                    if (m.RoleId == 5)
                    {
                        obj.Id = m.UserId;
                        obj.Name = m.UserName;
                        obj.Email = m.UserEmail;
                        obj.RoleId = m.RoleId;
                        obj.RoleName = m.Role.RoleName;

                        var eids = await db.Employees.Where(x => x.IsActive == true).Select(e => e.EmployeeId).ToListAsync();

                        var date_ = DateOnly.FromDateTime(DateTime.Today);

                        var fromDate = date_.AddDays(-2);

                        var att = await db.AttendanceLogs
                            .Where(x =>
                                x.EmployeeId.HasValue &&
                                eids.Contains(x.EmployeeId.Value) &&
                                x.AttendanceDate >= fromDate &&
                                x.AttendanceDate <= date_)
                            .GroupBy(x => x.EmployeeId.Value)
                            .ToDictionaryAsync(
                                g => g.Key,
                                g => g.ToList()
                            );


                        var employees = await db.Employees
                                        .AsNoTracking()
                                        .Include(x => x.Role)
                                        .Include(x => x.Locations)
                                        .Include(x => x.EmployeeFiles)
                                        .Where(x => x.IsActive)
                                        .ToListAsync();

                        obj.Employees = employees.Select(x => new RegisteredEmployeesData
                        {
                            EmployeeId = x.EmployeeId,
                            EmployeeName = x.EmployeeName,
                            EmployeeEmail = x.EmployeeEmail,
                            EmployeeCode = x.EmployeeCode,
                            MobileNumber = x.MobileNumber,
                            File = x.EmployeeFiles
                                .Select(f => new FileDto
                                {
                                    FileId = f.EmployeeFileId,
                                    FilePath = $"/images/employee/{x.EmployeeCode}/{f.FilePath}",
                                    Extension = f.Extension,
                                    Sid = x.EmployeeId,
                                    ActiveStatus = x.IsActive,
                                }).FirstOrDefault(),

                            attendance = att.TryGetValue(x.EmployeeId, out var logs)
                                ? logs.Select(a => new AttendanceDataLog
                                {
                                    AttendanceLogId = a.AttendanceLogId,
                                    AttendanceInLat = a.AttendanceInLat,
                                    AttendanceInLon = a.AttendanceInLon,
                                    TimeInLocationName = a.TimeInLocationName,
                                    TimeInAt = a.TimeInAt,
                                    TimeInMobile = a.TimeInMobile,
                                    TimeInImage = a.TimeInImage,
                                    TimeInBy = a.TimeInBy,
                                    TimeInType = a.TimeInType,
                                    AttendanceOutLat = a.AttendanceOutLat,
                                    AttendanceOutLon = a.AttendanceOutLon,
                                    TimeOutLocationName = a.TimeOutLocationName,
                                    TimeOutBy = a.TimeOutBy,
                                    TimeOutAt = a.TimeOutAt,
                                    TimeOutMobile = a.TimeOutMobile,
                                    TimeOutImage = a.TimeOutImage,
                                    TimeOutType = a.TimeOutType,
                                    AttendanceDate = a.AttendanceDate
                                }).ToList()
                                : new List<AttendanceDataLog>(),

                            Locations_ = x.Locations.Select(l => new UserLocationDto
                            {
                                LocationId = l.LocationId,
                                Latitude = l.Latitude,
                                Longitude = l.Longitude,
                                LocationType = l.LocationName,
                                Radius = l.Radius
                            }).ToList()
                        }).ToList();

                        //obj.Employees = db.Employees
                        //    .AsNoTracking()
                        //    .Include(x => x.Role)
                        //    .Include(x => x.Locations)
                        //    .Where(e => e.IsActive == true).Select(x => new RegisteredEmployeesData()
                        //    {

                        //        EmployeeId = x.EmployeeId,
                        //        EmployeeName = x.EmployeeName,
                        //        EmployeeEmail = x.EmployeeEmail,
                        //        EmployeeCode = x.EmployeeCode,
                        //        MobileNumber = x.MobileNumber,
                        //        File = x.EmployeeFiles
                        //        .Select(f => new FileDto
                        //        {
                        //            FileId = f.EmployeeFileId,
                        //            FilePath = $"/images/employee/{x.EmployeeCode}/{f.FilePath}",
                        //            Extension = f.Extension,
                        //            Sid = x.EmployeeId
                        //        }).FirstOrDefault(),

                        //        attendance = att.TryGetValue(x.EmployeeId, out var logs)
                        //                    ? logs.Select(a => new AttendanceDataLog
                        //                    {
                        //                        AttendanceLogId = a.AttendanceLogId,
                        //                        AttendanceInLat = a.AttendanceInLat,
                        //                        AttendanceInLon = a.AttendanceInLon,
                        //                        TimeInLocationName = a.TimeInLocationName,
                        //                        TimeInAt = a.TimeInAt,
                        //                        TimeInMobile = a.TimeInMobile,
                        //                        TimeInImage = a.TimeInImage,
                        //                        TimeInBy = a.TimeInBy,
                        //                        TimeInType = a.TimeInType,
                        //                        AttendanceOutLat = a.AttendanceOutLat,
                        //                        AttendanceOutLon = a.AttendanceOutLon,
                        //                        TimeOutLocationName = a.TimeOutLocationName,
                        //                        TimeOutBy = a.TimeOutBy,
                        //                        TimeOutAt = a.TimeOutAt,
                        //                        TimeOutMobile = a.TimeOutMobile,
                        //                        TimeOutImage = a.TimeOutImage,
                        //                        TimeOutType = a.TimeOutType,
                        //                        AttendanceDate = a.AttendanceDate
                        //                    }).ToList()
                        //                    : new List<AttendanceDataLog>(),
                        //        Locations_ = x.Locations.Select(l => new UserLocationDto()
                        //        {
                        //            LocationId = l.LocationId,
                        //            Latitude = l.Latitude,
                        //            Longitude = l.Longitude,
                        //            LocationType = l.LocationName,
                        //            Radius = l.Radius
                        //        }).ToList()
                        //    }).ToList();

                        return Json(new { obj, token = GenerateJwtToken(m.UserId, m.RoleId) });
                    }
                }

                return BadRequest(new { statusCode = 400, message = "Invalid user." });

            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("CheckLogout")]
        public async Task<IActionResult> CheckLogout(string email, string password)
        {
            try
            {
                var check = await db.Users.Where(x => x.UserEmail == email && x.UserPassword == Security.Encrypt(password)).FirstOrDefaultAsync();

                if (check == null)
                {
                    return NotFound(new { statusCode = 404, message = "Invalid email or password." });
                }

                return Json(true);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("RegisterPhotoFromHR")]
        public async Task<IActionResult> RegisterPhotoFromHR(IFormFile file, int employeeId)
        {
            try
            {

                if (file == null)
                {
                    return BadRequest(new { statusCode = 400, message = "No file uploaded." });
                }

                if (string.Equals(Path.GetExtension(file.FileName), ".mp4", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(new { statusCode = 400, message = "Invalid file format. Only .mp4 files are not allowed." });
                }

                var check = await db.EmployeeFiles.Where(x => x.EmployeeId == employeeId).AnyAsync();
                if (check)
                {
                    await db.EmployeeFiles.Where(x => x.EmployeeId == employeeId).ExecuteUpdateAsync(e => e.SetProperty(p => p.IsActive, false));
                }

                FileDto _ = new();
                string employeeCode = await db.Employees.Where(x => x.EmployeeId == employeeId).Select(x => x.EmployeeCode).FirstOrDefaultAsync();

                EmployeeFile file_ = new();
                file_.FilePath = await UploadDoc.UploadEmployeeImage(file, employeeCode);
                file_.Extension = Path.GetExtension(file.FileName);
                file_.CreatedAt = DateTime.Now;
                file_.EmployeeId = employeeId;
                file_.IsActive = true;

                await db.EmployeeFiles.AddAsync(file_);
                await db.SaveChangesAsync();

                _.FileId = file_.EmployeeFileId;
                _.FilePath = $"/images/employee/{employeeCode}/{file_.FilePath}";
                _.Extension = file_.Extension;


                return Json(_);
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        [HttpPost("RegisterPhoto")]
        public async Task<IActionResult> RegisterPhoto(IFormFile file)
        {
            try
            {
                var sid = int.Parse(claims["sid"]);
                var RoleId = int.Parse(claims["RoleId"]);

                if (file == null)
                {
                    return BadRequest(new { statusCode = 400, message = "No file uploaded." });
                }

                if (string.Equals(Path.GetExtension(file.FileName), ".mp4", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(new { statusCode = 400, message = "Invalid file format. Only .mp4 files are not allowed." });
                }

                FileDto _ = new();

                if (RoleId == 3)
                {
                    var check = await db.EmployeeFiles.Where(x => x.EmployeeId == sid).FirstOrDefaultAsync();
                    string employeeCode = await db.Employees.Where(x => x.EmployeeId == sid).Select(x => x.EmployeeCode).FirstOrDefaultAsync();

                    if (check == null)
                    {
                        EmployeeFile file_ = new();
                        file_.FilePath = await UploadDoc.UploadEmployeeImage(file, employeeCode);
                        file_.Extension = Path.GetExtension(file.FileName);
                        file_.CreatedAt = DateTime.Now;
                        file_.EmployeeId = sid;

                        await db.EmployeeFiles.AddAsync(file_);
                        await db.SaveChangesAsync();

                        _.FileId = file_.EmployeeFileId;
                        _.FilePath = $"/images/employee/{employeeCode}/{file_.FilePath}";
                        _.Extension = file_.Extension;

                        return Json(_);
                    }

                    check.FilePath = await UploadDoc.UploadEmployeeImage(file, employeeCode);
                    await db.SaveChangesAsync();

                    _.FileId = check.EmployeeFileId;
                    _.FilePath = $"/images/employee/{employeeCode}/{check.FilePath}";
                    _.Extension = check.Extension;

                    return Json(_);
                }

                var check_ = await db.UserFiles.Where(x => x.UserId == sid).FirstOrDefaultAsync();
                if (check_ == null)
                {
                    UserFile file_ = new();
                    file_.FilePath = await UploadDoc.UploadStaffImage(file, sid.ToString());
                    file_.Extension = Path.GetExtension(file.FileName);
                    file_.CreatedAt = DateTime.Now;
                    file_.UserId = sid;
                    await db.UserFiles.AddAsync(file_);
                    await db.SaveChangesAsync();

                    _.FileId = file_.UserFileId;
                    _.FilePath = $"/images/staff/{sid.ToString()}/{file_.FilePath}";
                    _.Extension = file_.Extension;

                    return Json(_);
                }

                check_.FilePath = await UploadDoc.UploadStaffImage(file, sid.ToString());
                await db.SaveChangesAsync();

                _.FileId = check_.UserFileId;
                _.FilePath = $"/images/staff/{sid.ToString()}/{check_.FilePath}";
                _.Extension = check_.Extension;

                return Json(_);
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        // CORRECTED: Removed async Task and duplicate copies
        private string GenerateJwtToken(int UserId, int RoleId)
        {
            string secretKey = configuration["JWTSettings:SecretKey"]?.ToString() ?? "0";
            string issuer = configuration["JWTSettings:Issuer"]?.ToString() ?? "0";
            string audience = configuration["JWTSettings:Audience"]?.ToString() ?? "0";

            var claims = new Claim[]
            {
                new(JwtRegisteredClaimNames.Sid, UserId.ToString()),
                //new("DepartmentId", DepartmentId.ToString()),
                new("RoleId", RoleId.ToString())
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.Now.AddYears(3),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}