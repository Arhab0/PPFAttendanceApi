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
    public class DepartmentController(ppfdbContext _context, ClaimsService _claims) : Controller
    {
        private readonly ppfdbContext db = _context;
        private readonly ClaimsService claims = _claims;

        [HttpPost("CreateDepartment")]
        public async Task<IActionResult> CreateDepartment(DepartmentDto dto)
        {
            try
            {
                var sid = int.Parse(claims["sid"]);
                if (string.IsNullOrWhiteSpace(dto.DepartmentName))
                {
                    return BadRequest(new { message = "Department name is required" });
                }

                var exists = await db.Departments.AnyAsync(x => x.DepartmentName.ToLower() == dto.DepartmentName.ToLower() && x.BranchId == dto.BranchId);
                if (exists)
                {
                    return BadRequest(new { message = "Department already exists in this branch." });
                }

                var dept = new Department
                {
                    DepartmentName = dto.DepartmentName,
                    CreatedAt = DateTime.Now,
                    CreatedBy = sid,
                    ParentDepartmentId = dto.ParentDepartmentId,
                    BranchId = dto.BranchId
                };

                await db.Departments.AddAsync(dept);
                await db.SaveChangesAsync();

                return Ok(new { statusCode = 200, message = "Department created successfully", departmentId = dept.DepartmentId });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Failed to create department", error = ex.Message });
            }
        }

        [HttpPost("UpdateDepartment")]
        public async Task<IActionResult> UpdateDepartment(DepartmentDto dto)
        {
            try
            {
                var sid = int.Parse(claims["sid"]);

                if (dto.DepartmentId == dto.ParentDepartmentId)
                {
                    return BadRequest(new { message = "A department cannot be its own parent" });
                }

                if (string.IsNullOrWhiteSpace(dto.DepartmentName))
                {
                    return BadRequest(new { message = "Department name is required" });
                }

                var dept = await db.Departments.FindAsync(dto.DepartmentId);
                if (dept == null)
                {
                    return NotFound(new { message = "Department not found" });
                }

                var exists = await db.Departments.AnyAsync(x => x.DepartmentName.ToLower() == dto.DepartmentName.ToLower() && x.DepartmentId != dto.DepartmentId && x.BranchId == dto.BranchId);
                if (exists)
                {
                    return BadRequest(new { message = "Another department with the same name already exists in this branch" });
                }

                dept.DepartmentName = dto.DepartmentName;
                dept.UpdatedAt = DateTime.Now;
                dept.ParentDepartmentId = dto.ParentDepartmentId;
                dept.BranchId = dto.BranchId;

                await db.SaveChangesAsync();
                return Ok(new { statusCode = 200, message = "Department updated successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Failed to update department", error = ex.Message });
            }
        }

        [HttpGet("GetDepartmentById")]
        public async Task<IActionResult> GetDepartmentById(int departmentId)
        {
            try
            {
                var dept = await db.Departments.Include(x => x.Branch).Include(x => x.ParentDepartment)
                    .Where(d => d.DepartmentId == departmentId)
                    .Select(d => new { d.DepartmentId, d.DepartmentName, d.ParentDepartmentId, ParentDepartmentName = d.ParentDepartment.DepartmentName, d.BranchId, d.Branch.BranchName })
                    .FirstOrDefaultAsync();
                if (dept == null)
                {
                    return NotFound(new { message = "Department not found" });
                }
                return Ok(new { statusCode = 200, department = dept });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Failed to fetch department", error = ex.Message });
            }
        }

        [HttpGet("GetDepartmentByBranchId")]
        public async Task<IActionResult> GetDepartmentByBranchId(int branchId)
        {
            try
            {
                var departments = await db.Departments.Where(x => x.BranchId == branchId).Include(x => x.ParentDepartment)
                    .Select(d => new { d.DepartmentId, d.DepartmentName, d.ParentDepartmentId, ParentDepartmentName = d.ParentDepartment.DepartmentName, d.BranchId })
                    .ToListAsync();
                return Ok(new { statusCode = 200, departments });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Failed to fetch departments", error = ex.Message });
            }
        }

        [HttpGet("GetAllDepartments")]
        public async Task<IActionResult> GetAllDepartments()
        {
            try
            {
                var departments = await db.Departments.Include(x => x.ParentDepartment).Include(x => x.Branch)
                    .Select(d => new { d.DepartmentId, d.DepartmentName, d.ParentDepartmentId, ParentDepartmentName = d.ParentDepartment.DepartmentName, d.BranchId, d.Branch.BranchName })
                    .ToListAsync();
                return Ok(new { statusCode = 200, departments });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Failed to fetch departments", error = ex.Message });
            }
        }

        [HttpGet("GetAllMembersOfDepartment")]
        public async Task<IActionResult> GetAllMembersOfDepartment(int departmentId)
        {
            try
            {
                var empids = await db.EmpUserDepartmentMappings.Where(x => x.DepartmentId == departmentId && x.Employee.IsActive == true).Select(x => x.EmployeeId).ToListAsync();

                var data = await db.Employees.AsNoTracking().Where(x => empids.Contains(x.EmployeeId))
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
                                x.EmployeeTypeId,
                                EmployeeType = x.EmployeeType.Type,
                                x.ShiftTypeId,
                                ShiftType = x.ShiftType.Type,
                                x.PaymentTypeId,
                                PaymentType = x.PaymentType.Type,
                            })
                            .ToListAsync();

                return Ok(new { statusCode = 200, data });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Failed to fetch department members", error = ex.Message });
            }
        }

        [HttpPost("DeActivateDepartment")]
        public async Task<IActionResult> DeActivateDepartment(int departmentId)
        {
            try
            {
                var check1 = await db.Departments.Where(x => x.ParentDepartmentId == departmentId && x.IsActive == true).CountAsync();
                var check2 = await db.EmpUserDepartmentMappings.Where(x => x.DepartmentId == departmentId && (x.Employee.IsActive == true || x.User.IsActive == true)).CountAsync();
                
                if(check1 > 0)
                {
                    return BadRequest(new { message = "Cannot deactivate department with active sub-departments" });
                }
                else if(check2 > 0)
                {
                    return BadRequest(new { message = "Cannot deactivate department with active users assigned" });
                }

                var _ = await db.Departments.Where(x => x.DepartmentId == departmentId).FirstOrDefaultAsync();
                _.IsActive = false;

                await db.SaveChangesAsync();
                return Ok(new { statusCode = 200, message = "Department deactivated successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Failed to deactivate department", error = ex.Message });
            }
        }
    }
}
