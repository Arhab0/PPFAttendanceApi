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
    public class UserController(ppfdbContext _context, ClaimsService _claims) : Controller
    {
        private readonly ppfdbContext db = _context;
        private readonly ClaimsService claims = _claims;


        [HttpGet("GetAllUsers")]
        public async Task<IActionResult> GetAllUsers()
        {
            try
            {
                var users = await db.Users
                    .Include(x => x.Role)
                    .Where(x => x.IsActive == true)
                    .Select(x => new
                    {
                        id = x.UserId,
                        name = x.UserName,
                        email = x.UserEmail,
                        roleId = x.RoleId,
                        roleName = x.Role.RoleName,
                        //latitude = x.Locations.Select(l => l.Latitude).FirstOrDefault(),
                        //longitude = x.Locations.Select(l => l.Longitude).FirstOrDefault(),
                        createdAt = x.CreatedAt,
                        type = "User"
                    })
                    .ToListAsync();

                return Ok(users);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("CreateUser")]
        public async Task<IActionResult> CreateUser(RegisterUserDto userDto)
        {
            await db.Database.BeginTransactionAsync();
            try
            {
                var sid = int.Parse(claims["sid"]);

                string encrypt = Security.Encrypt(userDto.Password);
                ActivityLog log = new();

                var check1 = await db.Users.Where(x => x.UserEmail.ToLower() == userDto.Email.ToLower()).AnyAsync();
                var check2 = await db.Employees.Where(x => x.EmployeeEmail.ToLower() == userDto.Email.ToLower()).AnyAsync();

                if (check1 || check2)
                {
                    return BadRequest(new { statusCode = 400, message = "User with this email already exists" });
                }

                if (userDto.RoleId == 1)
                {
                    User user = new()
                    {
                        UserName = userDto.Name,
                        UserEmail = userDto.Email,
                        UserPassword = encrypt,
                        RoleId = userDto.RoleId,
                        IsActive = true,
                        CreatedAt = DateTime.Now
                    };
                    await db.Users.AddAsync(user);
                    await db.SaveChangesAsync();

                    log.CreatedById = sid;
                    log.CreatedId = user.UserId;
                    log.CreatedAt = DateTime.Now;
                    log.Description = $"Super Admin created another Super Admin";
                }
                else if (userDto.RoleId == 4 || userDto.RoleId == 5)
                {
                    User user = new()
                    {
                        UserName = userDto.Name,
                        UserEmail = userDto.Email,
                        UserPassword = encrypt,
                        RoleId = userDto.RoleId,
                        IsActive = true,
                        CreatedAt = DateTime.Now
                    };
                    await db.Users.AddAsync(user);
                    await db.SaveChangesAsync();

                    List<Location> locations = new();
                    List<EmpUserBrDeptMapping> employeeBrDeptMappings = new();

                    if (userDto.BrDeptMapping.Count > 0)
                    {
                        var branchIds = userDto.BrDeptMapping
                                         .Select(x => x.BranchId)
                                         .Distinct()
                                         .ToList();

                        var branches = await db.Branches
                            .Where(b => branchIds.Contains(b.BranchId))
                            .Select(x => new
                            {
                                x.BranchId,
                                x.BranchName,
                                x.Latitude,
                                x.Longitude,
                                x.Radius
                            })
                            .ToListAsync();

                        foreach (var item in userDto.BrDeptMapping)
                        {
                            EmpUserBrDeptMapping mapping = new()
                            {
                                UserId = user.UserId,
                                BranchId = item.BranchId,
                                DepartmentId = item.DepartmentId
                            };
                            employeeBrDeptMappings.Add(mapping);
                        }

                        foreach (var item in branches)
                        {
                            Location location = new()
                            {
                                UserId = user.UserId,
                                Latitude = item.Latitude,
                                Longitude = item.Longitude,
                                LocationName = item.BranchName,
                                Radius = item.Radius,
                                CreatedAt = DateTime.Now
                            };
                            locations.Add(location);
                        }

                        await db.EmpUserBrDeptMappings.AddRangeAsync(employeeBrDeptMappings);
                        await db.Locations.AddRangeAsync(locations);
                    }

                    log.CreatedById = sid;
                    log.CreatedId = user.UserId;
                    log.CreatedAt = DateTime.Now;
                    log.Description = $"{(userDto.RoleId == 4 ? "HR" : "Attendance Manager")} created by Super Admin";
                }

                await db.ActivityLogs.AddAsync(log);
                await db.SaveChangesAsync();
                await db.Database.CommitTransactionAsync();

                return Ok(new { statusCode = 200, message = $"{(userDto.RoleId == 4 ? "HR" : "Attendance Manager")} created successfully" });
            }
            catch (Exception ex)
            {
                await db.Database.RollbackTransactionAsync();
                return Ok(new { statusCode = 500, message = $"An error occurred while creating the user", error = ex.Message });
            }
        }

        [HttpPost("DeActivateUser")]
        public async Task<IActionResult> DeActivateUser(int id, bool status)
        {
            await db.Database.BeginTransactionAsync();
            try
            {
                var sid = int.Parse(claims["sid"]);
                ActivityLog log = new();

                var user = await db.Users.Include(x => x.Role).Where(u => u.UserId == id).FirstOrDefaultAsync();
                if (user == null)
                {
                    return NotFound(new { statusCode = 404, message = $"user not found." });
                }

                user.IsActive = status;
                log.CreatedById = sid;
                log.CreatedId = user.UserId;
                log.CreatedAt = DateTime.Now;
                log.Description = $"{user.Role.RoleName} {(status == true ? "Activated" : "Deactivated")} by {(claims["RoleId"] == "1" ? "Super Admin" : "Manager")}";

                await db.ActivityLogs.AddAsync(log);
                await db.SaveChangesAsync();
                await db.Database.CommitTransactionAsync();

                return Ok(new { statusCode = 200, message = $"{user.Role.RoleName} {(status == true ? "Activated" : "Deactivated")} successfully" });
            }
            catch (Exception ex)
            {
                await db.Database.RollbackTransactionAsync();
                return BadRequest(new { statusCode = 500, message = $"An error occurred while {(status == true ? "activating" : "deactivating")}", error = ex.Message });
            }
        }
    }
}
