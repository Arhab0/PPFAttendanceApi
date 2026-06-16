using PPFAttendanceApi.Dto;
using PPFAttendanceApi.Helper;
using PPFAttendanceApi.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace PPFAttendanceApi.Controllers
{
    [Route("api/[controller]")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class EmployeeController(ppfdbContext _context, ClaimsService _claims) : Controller
    {
        private readonly ppfdbContext db = _context;
        private readonly ClaimsService claims = _claims;

        [HttpPost("AddEmployee")]
        public async Task<IActionResult> AddEmployee(RegisterUserDto dto)
        {
            await db.Database.BeginTransactionAsync();
            try
            {
                var sid = int.Parse(claims["sid"]);
                var roleId = int.Parse(claims["RoleId"]);

                if (await db.Employees.AnyAsync(e => e.EmployeeEmail.Trim() == dto.Email.Trim()))
                {
                    return BadRequest(new { message = "Employee with this Email already exists." });
                }

                if (dto.BrDeptMapping.Count == 0)
                {
                    return BadRequest(new { statusCode = 400, message = "Required entries are missing." });
                }

                var employeeCodeCheck = await db.Employees.AnyAsync(e => e.EmployeeCode == "EMP-" + dto.EmployeeId.ToString());

                if (employeeCodeCheck)
                {
                    return BadRequest(new { message = "Employee with this EmployeeCode already exists." });
                }

                var password = Security.Encrypt(dto.Password);
                var employee = new Employee
                {
                    EmployeeName = dto.Name,
                    EmployeeFatherName = dto.FatherName,
                    EmployeeCode = "EMP-" + dto.EmployeeId.ToString(),
                    Cnic = dto.Cnic,
                    JobTitle = dto.JobTitle,
                    MobileNumber = dto.MobileNumber,
                    EmployeeEmail = dto.Email,
                    EmployeePassword = password,
                    RoleId = dto.RoleId,
                    IsActive = true,
                    PaymentTypeId = dto.PaymentTypeId,
                    ShiftTypeId = dto.ShiftTypeId,
                    EmployeeTypeId = dto.EmployeeTypeId,
                    CreatedAt = DateTime.Now
                };
                db.Employees.Add(employee);
                await db.SaveChangesAsync();

                List<Location> locations = new();
                List<EmpUserBrDeptMapping> employeeBrDeptMappings = new();

                var branchIds = dto.BrDeptMapping
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

                foreach (var item in dto.BrDeptMapping)
                {
                    EmpUserBrDeptMapping mapping = new()
                    {
                        EmployeeId = employee.EmployeeId,
                        BranchId = item.BranchId,
                        DepartmentId = item.DepartmentId
                    };
                    employeeBrDeptMappings.Add(mapping);
                }

                foreach (var item in branches)
                {
                    Location location = new()
                    {
                        EmployeeId = employee.EmployeeId,
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

                await db.ActivityLogs.AddAsync(new()
                {
                    CreatedById = sid,
                    CreatedId = employee.EmployeeId,
                    CreatedAt = DateTime.Now,
                    Description = $"Employee created by {(roleId == 1 ? "Super Admin" : "HR")}"
                });

                await db.SaveChangesAsync();
                await db.Database.CommitTransactionAsync();
                return Ok(new { statusCode = 200, message = "Employee added successfully" });
            }
            catch (Exception e)
            {
                await db.Database.RollbackTransactionAsync();
                return BadRequest(e.Message);
            }
        }

        [HttpPost("UpdateEmployee")]
        public async Task<IActionResult> UpdateEmployee(RegisterUserDto dto)
        {
            await db.Database.BeginTransactionAsync();
            try
            {
                var check = await db.Employees.Where(x => x.EmployeeId != dto.sid && x.EmployeeEmail == dto.Email).FirstOrDefaultAsync();

                if (check != null)
                {
                    return BadRequest(new { statusCode = 400, message = "Employee with this Email already exists." });
                }

                if (dto.BrDeptMapping.Count == 0)
                {
                    return BadRequest(new { statusCode = 400, message = "Required entries are missing." });
                }

                var employee = await db.Employees.Where(x => x.EmployeeId == dto.sid).FirstOrDefaultAsync();

                employee.EmployeeName = dto.Name;
                employee.EmployeeFatherName = dto.FatherName;
                employee.Cnic = dto.Cnic;
                employee.JobTitle = dto.JobTitle;
                employee.MobileNumber = dto.MobileNumber;
                employee.EmployeeEmail = dto.Email;
                employee.RoleId = dto.RoleId;
                employee.PaymentTypeId = dto.PaymentTypeId;
                employee.ShiftTypeId = dto.ShiftTypeId;
                employee.EmployeeTypeId = dto.EmployeeTypeId;
                employee.UpdatedAt = DateTime.Now;

                if (!string.IsNullOrEmpty(dto.Password))
                {
                    var password = Security.Encrypt(dto.Password);
                    employee.EmployeePassword = password;
                }

                await db.EmpUserBrDeptMappings.Where(x => x.EmployeeId == dto.sid).ExecuteDeleteAsync();

                List<Location> locations = new();
                List<EmpUserBrDeptMapping> employeeBrDeptMappings = new();

                var branches = await db.Branches.Where(b => dto.BrDeptMapping.Select(i => i.BranchId).ToList().Contains(b.BranchId)).Select(x => new { x.BranchName, x.Latitude, x.Longitude, x.Radius }).ToListAsync();

                foreach (var item in dto.BrDeptMapping)
                {
                    EmpUserBrDeptMapping mapping = new()
                    {
                        EmployeeId = employee.EmployeeId,
                        BranchId = item.BranchId,
                        DepartmentId = item.DepartmentId
                    };
                    employeeBrDeptMappings.Add(mapping);
                }

                foreach (var item in branches)
                {
                    Location location = new()
                    {
                        EmployeeId = employee.EmployeeId,
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
                await db.SaveChangesAsync();

                await db.Database.CommitTransactionAsync();
                return Json(new { statusCode = 200, message = "Employee Updated Successfully." });
            }
            catch (Exception e)
            {
                await db.Database.RollbackTransactionAsync();
                return Json(e.Message);
            }
        }

        [HttpGet("GetAllEmployees")]
        public async Task<IActionResult> GetAllEmployees()
        {
            try
            {
                var data = await db.Employees.AsNoTracking()
                            .Include(x => x.EmpUserBrDeptMappings)
                            .Include(x => x.EmployeeType)
                            .Include(x => x.ShiftType)
                            .Include(x => x.PaymentType)
                            .Select(x => new
                            {
                                x.EmployeeId,
                                x.EmployeeName,
                                x.EmployeeFatherName,
                                x.Cnic,
                                x.MobileNumber,
                                x.JobTitle,
                                x.EmployeeEmail,
                                x.EmployeeCode,
                                x.IsActive,
                                x.EmployeeTypeId,
                                EmployeeType = x.EmployeeType.Type,
                                x.ShiftTypeId,
                                ShiftType = x.ShiftType.Type,
                                x.PaymentTypeId,
                                PaymentType = x.PaymentType.Type,
                                mapping = x.EmpUserBrDeptMappings.Select(x => new { x.BrDeptMappingId, x.BranchId, x.Branch.BranchName, x.DepartmentId, x.Department.DepartmentName }).ToList()
                            }).ToListAsync();

                return Json(data);
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        [HttpGet("GetEmployeeById")]
        public async Task<IActionResult> GetEmployeeById(int employeeId)
        {
            try
            {
                var data = await db.Employees.AsNoTracking()
                            .Where(x => x.EmployeeId == employeeId)
                            .Include(x => x.EmpUserBrDeptMappings)
                            .Include(x => x.EmployeeType)
                            .Include(x => x.ShiftType)
                            .Include(x => x.PaymentType)
                            .Select(x => new
                            {
                                x.EmployeeId,
                                x.EmployeeName,
                                x.EmployeeFatherName,
                                x.Cnic,
                                x.MobileNumber,
                                x.JobTitle,
                                x.EmployeeEmail,
                                x.EmployeeCode,
                                x.IsActive,
                                x.EmployeeTypeId,
                                EmployeeType = x.EmployeeType.Type,
                                x.ShiftTypeId,
                                ShiftType = x.ShiftType.Type,
                                x.PaymentTypeId,
                                PaymentType = x.PaymentType.Type,
                                mapping = x.EmpUserBrDeptMappings.Select(x => new { x.BrDeptMappingId, x.BranchId, x.Branch.BranchName, x.DepartmentId, x.Department.DepartmentName }).ToList()
                            })
                            .FirstOrDefaultAsync();

                return Json(data);
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        [HttpPost("ChangeEmployeeActivationStatus")]
        public async Task<IActionResult> ChangeEmployeeActivationStatus(int employeeId,bool status)
        {
            try
            {
                var employee = await db.Employees.Where(x => x.EmployeeId == employeeId).FirstOrDefaultAsync();
                employee.IsActive = status;
                await db.SaveChangesAsync();
                return Json(new { statusCode = 200, message = $"Employee {(status == false ? "Deactivated" : "Activated")} Successfully." });
            }
            catch (Exception e)
            {
                return Json(e.Message);
            }
        }
    }
}
