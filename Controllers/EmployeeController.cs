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
                var userClaimId = int.Parse(claims["sid"]);
                var roleId = int.Parse(claims["RoleId"]);

                if (!string.IsNullOrEmpty(dto.Email))
                {
                    if (await db.Employees.AnyAsync(e => e.EmployeeEmail.Trim() == dto.Email.Trim()))
                    {
                        return BadRequest(new { statusCode = 400, message = "Employee with this Email already exists." });
                    }
                }

                var cnicCheck = await db.Employees.AnyAsync(e => e.Cnic == dto.Cnic);
                var phoneNoCheck = await db.Employees.AnyAsync(e => e.MobileNumber == dto.MobileNumber);
                if (cnicCheck)
                {
                    return BadRequest(new { statusCode = 400, message = "Employee with this CNIC already exists." });
                }

                if (phoneNoCheck)
                {
                    return BadRequest(new { statusCode = 400, message = "Employee with this PhoneNo already exists." });
                }

                if (dto.BrDeptMapping.Count(x => x.IsPrimaryBranch) > 1)
                {
                    return BadRequest(new { statusCode = 400, message = "Primary branch can only be one." });
                }

                if (dto.BrDeptMapping.Count == 0)
                {
                    return BadRequest(new { statusCode = 400, message = "Required entries are missing." });
                }

                //var employeeCodeCheck = await db.Employees.AnyAsync(e => e.EmployeeCode == "EMP-" + dto.EmployeeCodeNo.ToString());

                //if (employeeCodeCheck)
                //{
                //    return BadRequest(new { message = "Employee with this EmployeeCode already exists." });
                //}

                var employee = new Employee
                {
                    EmployeeName = dto.Name,
                    EmployeeFatherName = dto.FatherName,
                    //EmployeeCode = "EMP-" + dto.EmployeeCodeNo.ToString(),
                    Cnic = dto.Cnic,
                    JobTitle = dto.JobTitle,
                    MobileNumber = dto.MobileNumber,
                    EmergencyContact = dto.EmergencyContact,
                    RoleId = dto.RoleId,
                    IsActive = true,
                    PaymentTypeId = dto.PaymentTypeId,
                    ShiftTypeId = dto.ShiftTypeId,
                    EmployeeTypeId = dto.EmployeeTypeId,
                    CreatedAt = DateTime.Now
                };
                db.Employees.Add(employee);
                await db.SaveChangesAsync();

                var count = await db.Employees.CountAsync();
                employee.EmployeeCode = "EMP-" + count.ToString("D4");

                if (!string.IsNullOrEmpty(dto.Password) && !string.IsNullOrEmpty(dto.Email))
                {
                    var password = Security.Encrypt(dto.Password);
                    employee.EmployeePassword = password;
                    employee.EmployeeEmail = dto.Email;
                }

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
                        DepartmentId = item.DepartmentId,
                        IsPrimaryBranch = item.IsPrimaryBranch
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
                    CreatedById = userClaimId,
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
                
                if (dto.BrDeptMapping.Count(x => x.IsPrimaryBranch) > 1)
                {
                    return BadRequest(new { statusCode = 400, message = "Primary branch can only be one." });
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
                employee.EmergencyContact = dto.EmergencyContact;
                employee.RoleId = dto.RoleId;
                employee.PaymentTypeId = dto.PaymentTypeId;
                employee.ShiftTypeId = dto.ShiftTypeId;
                employee.EmployeeTypeId = dto.EmployeeTypeId;
                employee.UpdatedAt = DateTime.Now;

                if (!string.IsNullOrEmpty(dto.Password) && !string.IsNullOrEmpty(dto.Email))
                {
                    var check = await db.Employees.Where(x => x.EmployeeId != dto.sid && x.EmployeeEmail == dto.Email).FirstOrDefaultAsync();

                    if (check != null)
                    {
                        return BadRequest(new { statusCode = 400, message = "Employee with this Email already exists." });
                    }
                    var password = Security.Encrypt(dto.Password);
                    employee.EmployeePassword = password;
                    employee.EmployeeEmail = dto.Email;
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
                        DepartmentId = item.DepartmentId,
                        IsPrimaryBranch = item.IsPrimaryBranch,
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
                return BadRequest(e.Message);
            }
        }

        [HttpGet("GetAllEmployeesList")]
        public async Task<IActionResult> GetAllEmployeesList()
        {
            try
            {
                var empIds = await db.EmpUserBrDeptMappings.Where(x => x.Employee.IsActive == true).Select(x => x.EmployeeId).Distinct().ToListAsync();
                var data = await db.Employees.AsNoTracking()
                            .Where(e => empIds.Contains(e.EmployeeId))
                            .Include(x => x.EmpUserBrDeptMappings)
                                .ThenInclude(x => x.Branch)
                            .Include(x => x.EmployeeType)
                            .Include(x => x.ShiftType)
                            .Include(x => x.PaymentType)
                            .Include(x => x.EmployeeFiles)
                            .Include(x=>x.Role)
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
                                x.RoleId,
                                x.Role.RoleName,
                                MainBranch = x.EmpUserBrDeptMappings.Where(x => x.IsPrimaryBranch == true).Select(x => x.Branch.BranchName).FirstOrDefault(),
                                DepartmentName = x.EmpUserBrDeptMappings.Where(x => x.IsPrimaryBranch == true).Select(x => x.Department.DepartmentName).FirstOrDefault(),
                                OtherBranches = string.Join(",", x.EmpUserBrDeptMappings.Where(x => x.IsPrimaryBranch == false).Select(x => x.Branch.BranchName)),
                                mapping = x.EmpUserBrDeptMappings.Select(x => new { x.BrDeptMappingId, x.BranchId, x.Branch.BranchName, x.DepartmentId, x.Department.DepartmentName, x.IsPrimaryBranch }).ToList(),
                                IsFaceRegistered = x.EmployeeFiles.Any()
                            })
                            .OrderBy(x => x.EmployeeId)
                            .ToListAsync();

                return Json(data);
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        [HttpGet("GetAllEmployees")]
        public async Task<IActionResult> GetAllEmployees(int BranchId, int DepartmentId)
        {
            try
            {
                var empIds = await db.EmpUserBrDeptMappings.Where(x => x.BranchId == BranchId && x.DepartmentId == DepartmentId && x.Employee.IsActive == true).Select(x => x.EmployeeId).Distinct().ToListAsync();
                var data = await db.Employees.AsNoTracking()
                            .Where(e => empIds.Contains(e.EmployeeId))
                            .Include(x => x.EmpUserBrDeptMappings)
                                .ThenInclude(x => x.Branch)
                            .Include(x => x.EmployeeType)
                            .Include(x => x.ShiftType)
                            .Include(x => x.PaymentType)
                            .Include(x => x.EmployeeFiles)
                            .Include(x=>x.Role)
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
                                x.RoleId,
                                x.Role.RoleName,
                                MainBranch = x.EmpUserBrDeptMappings.Where(x => x.IsPrimaryBranch == true).Select(x => x.Branch.BranchName).FirstOrDefault(),
                                DepartmentName = x.EmpUserBrDeptMappings.Where(x => x.IsPrimaryBranch == true).Select(x => x.Department.DepartmentName).FirstOrDefault(),
                                OtherBranches = string.Join(",", x.EmpUserBrDeptMappings.Where(x => x.IsPrimaryBranch == false).Select(x => x.Branch.BranchName)),
                                mapping = x.EmpUserBrDeptMappings.Select(x => new { x.BrDeptMappingId, x.BranchId, x.Branch.BranchName, x.DepartmentId, x.Department.DepartmentName, x.IsPrimaryBranch }).ToList(),
                                IsFaceRegistered = x.EmployeeFiles.Any()
                            })
                            .OrderBy(x => x.EmployeeId)
                            .ToListAsync();

                return Json(data);
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        [HttpGet("GetEmployeeById")]
        public async Task<IActionResult> GetEmployeeById(int employeeId, DateTime? fromDate, DateTime? toDate, int Page = 1, int PageSize = 15)
        {
            try
            {
                var startDate = fromDate?.Date ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                var endDate = toDate?.Date.AddDays(1) ?? startDate.AddMonths(1);

                var data = await db.Employees
                            .Where(x => x.EmployeeId == employeeId)
                            .Include(x => x.EmpUserBrDeptMappings)
                            .Include(x => x.EmployeeType)
                            .Include(x => x.ShiftType)
                            .Include(x => x.PaymentType)
                            .Include(x=>x.Role)
                            .Select(x => new
                            {
                                x.EmployeeId,
                                x.EmployeeName,
                                x.CreatedAt,
                                x.EmployeeFatherName,
                                x.Cnic,
                                x.MobileNumber,
                                x.JobTitle,
                                x.EmployeeEmail,
                                x.EmployeeCode,
                                x.IsActive,
                                x.EmergencyContact,
                                x.EmployeeTypeId,
                                EmployeeType = x.EmployeeType.Type,
                                x.ShiftTypeId,
                                ShiftType = x.ShiftType.Type,
                                x.PaymentTypeId,
                                x.RoleId,
                                x.Role.RoleName,
                                PaymentType = x.PaymentType.Type,
                                MainBranch = x.EmpUserBrDeptMappings.Where(x => x.IsPrimaryBranch == true).Select(x => x.Branch.BranchName).FirstOrDefault(),
                                Department = x.EmpUserBrDeptMappings.Where(x => x.IsPrimaryBranch == true).First().Department.DepartmentName,
                                mapping = x.EmpUserBrDeptMappings.Select(x => new { x.BrDeptMappingId, x.BranchId, x.Branch.BranchName, x.DepartmentId, x.Department.DepartmentName, x.IsPrimaryBranch }).ToList(),
                                IsFaceRegistered = x.EmployeeFiles.Any()
                            })
                            .FirstOrDefaultAsync();

                IQueryable<AttendanceLog> query = db.AttendanceLogs.AsNoTracking().Include(x => x.Employee).ThenInclude(x => x.ShiftType)
                    .Where(x => x.EmployeeId == employeeId
                        && (x.TimeInAt ?? x.TimeInMobile ?? x.TimeInImage) >= startDate
                        && (x.TimeInAt ?? x.TimeInMobile ?? x.TimeInImage) < endDate
                    );

                var logs = await query
                    .OrderBy(x => x.TimeInAt ?? x.TimeInMobile ?? x.TimeInImage)
                    .Skip((Page - 1) * PageSize)
                    .Take(PageSize)
                    .ToListAsync();

                var attendance = logs.Select(log =>
                {
                    var date_in = log.TimeInAt ?? log.TimeInMobile ?? log.TimeInImage;
                    var date_out = log.TimeOutAt ?? log.TimeOutMobile ?? log.TimeOutImage;
                    TimeSpan? workedDuration = (date_in.HasValue && date_out.HasValue)
                                            ? date_out.Value - date_in.Value
                                            : null;

                    string workedHours = workedDuration.HasValue
                        ? $"{(int)workedDuration.Value.TotalHours}h {workedDuration.Value.Minutes}m"
                        : "Incomplete";

                    double Difference = workedDuration.HasValue ? workedDuration.Value.TotalHours - log.Employee.ShiftType.ShiftHours : 0;

                    return new
                    {
                        EmployeeId = employeeId,
                        EmployeeName = data.EmployeeName,
                        EmployeeCode = data.EmployeeCode,
                        AttendanceLogId = log.AttendanceLogId,
                        AttendanceDate = log.AttendanceDate,
                        CheckInAt = date_in,
                        CheckInLocation = log.TimeInLocationName,
                        CheckOutAt = date_out,
                        CheckOutLocation = log.TimeOutLocationName,
                        log.Employee.ShiftType.ShiftHours,
                        PresentedHours = workedHours,
                        Difference,
                    };
                }).ToList();

                var FinalResult = new
                {
                    data,
                    attendance
                };

                return Json(FinalResult);
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        [HttpPost("ChangeEmployeeActivationStatus")]
        public async Task<IActionResult> ChangeEmployeeActivationStatus(int employeeId, bool status)
        {
            try
            {
                var sid = int.Parse(claims["sid"]);
                ActivityLog log = new();

                var employee = await db.Employees.Where(x => x.EmployeeId == employeeId).FirstOrDefaultAsync();
                employee.IsActive = status;


                log.CreatedById = sid;
                log.CreatedId = employee.EmployeeId;
                log.CreatedAt = DateTime.Now;
                log.Description = $"Employee {(status == true ? "Activated" : "Deactivated")} by {(claims["RoleId"] == "1" ? "Super Admin" : "HR")}";

                await db.ActivityLogs.AddAsync(log);
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
