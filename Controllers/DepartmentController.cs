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
            catch (Exception e)
            {
                return BadRequest(e.Message);
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
            catch (Exception e)
            {
                return BadRequest(e.Message);
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
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        // for dropdown
        [HttpGet("GetParentDepartmentByBranchId")]
        public async Task<IActionResult> GetParentDepartmentByBranchId(int branchId)
        {
            try
            {
                var activeChildIds = db.Departments.AsNoTracking()
                                    .Where(x => x.IsActive && x.ParentDepartmentId != null && x.BranchId == branchId)
                                    .Select(x => x.ParentDepartmentId).ToList();

                var departments = await db.Departments
                    .AsNoTracking()
                    .Where(x => x.IsActive && x.BranchId == branchId &&
                                (x.ParentDepartmentId == null || activeChildIds.Contains(x.DepartmentId)))
                    .Select(d => new
                    {
                        d.DepartmentId,
                        d.DepartmentName,
                    })
                    .ToListAsync();
                return Ok(new { statusCode = 200, departments });
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        // for dropdown
        [HttpGet("GetSubDepartmentsByParentId")]
        public async Task<IActionResult> GetSubDepartmentsByParentId(int parentId)
        {
            try
            {
                var departments = await db.Departments.AsNoTracking().Where(x => x.ParentDepartmentId == parentId && x.IsActive == true)
                    .Select(d => new
                    {
                        d.DepartmentId,
                        d.DepartmentName,
                    })
                    .ToListAsync();

                return Ok(new { statusCode = 200, departments });
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }
        [HttpGet("GetDepartmentByBranchId")]
        public async Task<IActionResult> GetDepartmentByBranchId(int branchId)
        {
            try
            {
                var departments = await db.Departments
                    .AsNoTracking()
                    .Where(x => x.BranchId == branchId && x.IsActive)
                    .Select(d => new
                    {
                        d.DepartmentId,
                        d.DepartmentName,
                        d.BranchId,
                        d.ParentDepartmentId,

                        ParentDepartmentName = d.ParentDepartment != null
                            ? d.ParentDepartment.DepartmentName
                            : null,

                        d.Branch.BranchName,

                        DirectCount = d.EmpUserBrDeptMappings.Count(m =>
                            (m.EmployeeId != null && m.Employee.IsActive) ||
                            (m.UserId != null && m.User.IsActive))
                    })
                    .ToListAsync();

                var counts = await db.Database.SqlQueryRaw<DeptCount>(@"
                        WITH RecursiveDept AS (
                            -- Anchor: start from each department itself
                            SELECT DepartmentId AS RootId, DepartmentId AS ChildId
                            FROM Departments
                            WHERE BranchId = {0} AND IsActive = 1

                            UNION ALL

                            -- Recursive: go deeper into children
                            SELECT r.RootId, d.DepartmentId
                            FROM Departments d
                            INNER JOIN RecursiveDept r ON d.ParentDepartmentId = r.ChildId
                            WHERE d.IsActive = 1
                        )
                        SELECT 
                            r.RootId AS DepartmentId,
                            COUNT(DISTINCT m.EmpUserBrDeptMappingId) AS TotalCount
                        FROM RecursiveDept r
                        LEFT JOIN EmpUserBrDeptMappings m ON m.DepartmentId = r.ChildId
                        LEFT JOIN Employees e ON m.EmployeeId = e.EmployeeId AND e.IsActive = 1
                        LEFT JOIN Users u ON m.UserId = u.UserId AND u.IsActive = 1
                        WHERE (m.EmployeeId IS NOT NULL AND e.EmployeeId IS NOT NULL)
                           OR (m.UserId IS NOT NULL AND u.UserId IS NOT NULL)
                           OR m.EmpUserBrDeptMappingId IS NULL
                        GROUP BY r.RootId
                           ", branchId).ToListAsync();

                var countMap = counts.ToDictionary(x => x.DepartmentId, x => x.TotalCount);

                var result = departments.Select(d => new
                {
                    d.DepartmentId,
                    d.DepartmentName,
                    d.BranchId,
                    d.ParentDepartmentId,
                    d.ParentDepartmentName,
                    d.BranchName,
                    TotalCount = countMap.TryGetValue(d.DepartmentId, out var count) ? count : 0
                }).ToList();

                return Ok(new { statusCode = 200, departments = result });
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        [HttpGet("GetAllMembersOfDepartment")]
        public async Task<IActionResult> GetAllMembersOfDepartment(int departmentId)
        {
            try
            {
                var empids = await db.EmpUserBrDeptMappings.Where(x => x.DepartmentId == departmentId && x.Employee.IsActive == true).Select(x => x.EmployeeId).ToListAsync();

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
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        [HttpPost("DeActivateDepartment")]
        public async Task<IActionResult> DeActivateDepartment(int departmentId)
        {
            try
            {
                var check1 = await db.Departments.Where(x => x.ParentDepartmentId == departmentId && x.IsActive == true).CountAsync();
                var check2 = await db.EmpUserBrDeptMappings.Where(x => x.DepartmentId == departmentId && (x.Employee.IsActive == true || x.User.IsActive == true)).CountAsync();

                if (check1 > 0)
                {
                    return BadRequest(new { message = "Cannot deactivate department with active sub-departments" });
                }
                else if (check2 > 0)
                {
                    return BadRequest(new { message = "Cannot deactivate department with active users assigned" });
                }

                var _ = await db.Departments.Where(x => x.DepartmentId == departmentId).FirstOrDefaultAsync();
                _.IsActive = false;

                await db.SaveChangesAsync();
                return Ok(new { statusCode = 200, message = "Department deactivated successfully" });
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }
    }
    public class DeptCount
    {
        public int DepartmentId { get; set; }
        public int TotalCount { get; set; }
    }
}
