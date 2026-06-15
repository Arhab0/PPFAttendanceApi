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

                if (dto.BranchIds.Count == 0 || dto.DepartmentIds.Count == 0)
                {
                    return BadRequest(new { statusCode = 400, message = "Required entries are missing." });
                }


                var password = Security.Encrypt(dto.Password);
                var employee = new Employee
                {
                    EmployeeName = dto.Name,
                    EmployeeFatherName = dto.FatherName,
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

                string employeeCount = db.Employees.Count().ToString("D4");
                employee.EmployeeCode = $"EMP-{employeeCount}";

                List<Location> locations = new();
                List<EmpUserBranchMapping> employeeBranchMappings = new();
                List<EmpUserDepartmentMapping> employeeDepartmentMappings = new();

                var branches = await db.Branches.Where(b => dto.BranchIds.Contains(b.BranchId)).Select(x => new { x.BranchName, x.Latitude, x.Longitude, x.Radius }).ToListAsync();

                foreach (var branchId in dto.BranchIds)
                {
                    EmpUserBranchMapping mapping = new()
                    {
                        EmployeeId = employee.EmployeeId,
                        BranchId = branchId
                    };
                    employeeBranchMappings.Add(mapping);
                }

                foreach (var departmentId in dto.DepartmentIds)
                {
                    EmpUserDepartmentMapping mapping = new()
                    {
                        EmployeeId = employee.EmployeeId,
                        DepartmentId = departmentId
                    };
                    employeeDepartmentMappings.Add(mapping);
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

                await db.EmpUserBranchMappings.AddRangeAsync(employeeBranchMappings);
                await db.EmpUserDepartmentMappings.AddRangeAsync(employeeDepartmentMappings);
                await db.Locations.AddRangeAsync(locations);
                await db.SaveChangesAsync();

                await db.ActivityLogs.AddAsync(new()
                {
                    CreatedById = sid,
                    CreatedId = employee.EmployeeId,
                    CreatedAt = DateTime.Now,
                    Description = $"Employee created by {(roleId == 1 ? "Super Admin" : "HR")}"
                });

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

                if (dto.BranchIds.Count == 0 || dto.DepartmentIds.Count == 0)
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

                await db.EmpUserDepartmentMappings.Where(x => x.EmployeeId == dto.sid).ExecuteDeleteAsync();
                await db.EmpUserBranchMappings.Where(x => x.EmployeeId == dto.sid).ExecuteDeleteAsync();

                List<Location> locations = new();
                List<EmpUserBranchMapping> employeeBranchMappings = new();
                List<EmpUserDepartmentMapping> employeeDepartmentMappings = new();

                var branches = await db.Branches.Where(b => dto.BranchIds.Contains(b.BranchId)).Select(x => new { x.BranchName, x.Latitude, x.Longitude, x.Radius }).ToListAsync();

                foreach (var branchId in dto.BranchIds)
                {
                    EmpUserBranchMapping mapping = new()
                    {
                        EmployeeId = employee.EmployeeId,
                        BranchId = branchId
                    };
                    employeeBranchMappings.Add(mapping);
                }

                foreach (var departmentId in dto.DepartmentIds)
                {
                    EmpUserDepartmentMapping mapping = new()
                    {
                        EmployeeId = employee.EmployeeId,
                        DepartmentId = departmentId
                    };
                    employeeDepartmentMappings.Add(mapping);
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

                await db.EmpUserBranchMappings.AddRangeAsync(employeeBranchMappings);
                await db.EmpUserDepartmentMappings.AddRangeAsync(employeeDepartmentMappings);
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
                            .Include(x => x.EmpUserBranchMappings).ThenInclude(x => x.Branch)
                            .Include(x => x.EmpUserDepartmentMappings).ThenInclude(x => x.Department)
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
                                Branches = x.EmpUserBranchMappings.Select(b => new { b.BranchId, b.Branch.BranchName }),
                                Departments = x.EmpUserDepartmentMappings.Select(d => new { d.DepartmentId, d.Department.DepartmentName })
                            })
                            .ToListAsync();
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
                            .Include(x => x.EmpUserBranchMappings).ThenInclude(x => x.Branch)
                            .Include(x => x.EmpUserDepartmentMappings).ThenInclude(x => x.Department)
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
                                Branches = x.EmpUserBranchMappings.Select(b => new { b.BranchId, b.Branch.BranchName }),
                                Departments = x.EmpUserDepartmentMappings.Select(d => new { d.DepartmentId, d.Department.DepartmentName })
                            })
                            .FirstOrDefaultAsync();

                return Json(data);
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        [HttpPost("DeActivateEmployee")]
        public async Task<IActionResult> DeActivateEmployee(int employeeId)
        {
            try
            {
                var employee = await db.Employees.Where(x => x.EmployeeId == employeeId).FirstOrDefaultAsync();
                employee.IsActive = false;
                await db.SaveChangesAsync();
                return Json(new { statusCode = 200, message = "Employee Deactivated Successfully." });
            }
            catch (Exception e)
            {
                return Json(e.Message);
            }
        }
    }
}
