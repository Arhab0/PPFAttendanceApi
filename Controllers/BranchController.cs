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
    public class BranchController(ppfdbContext _context, ClaimsService _claims) : Controller
    {
        private readonly ppfdbContext db = _context;
        private readonly ClaimsService claims = _claims;

        [HttpPost("AddBranch")]
        public async Task<IActionResult> AddBranch(BranchDto obj)
        {
            try
            {
                var sid = int.Parse(claims["sid"]);
                if (string.IsNullOrWhiteSpace(obj.BranchName))
                {
                    return BadRequest(new { statusCode = 400, message = "Branch name is required" });
                }
                var exists = await db.Branches.AnyAsync(x => x.BranchName.ToLower() == obj.BranchName.ToLower());
                if (exists)
                {
                    return BadRequest(new { statusCode = 400, message = "Branch already exists" });
                }

                Branch branch = new();

                branch.BranchName = obj.BranchName;
                branch.Latitude = obj.Latitude;
                branch.Longitude = obj.Longitude;
                branch.CreatedAt = DateTime.Now;
                branch.CreatedBy = sid;
                branch.IsActive = true;
                branch.Radius = obj.Radius;

                await db.Branches.AddAsync(branch);
                await db.SaveChangesAsync();
                return Ok(new { statusCode = 200, message = "Branch added successfully" });
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        [HttpPost("UpdateBranch")]
        public async Task<IActionResult> UpdateBranch(BranchDto obj)
        {
            try
            {
                if (obj.BranchId == 0 || obj.BranchId == null)
                {
                    return BadRequest(new { statusCode = 400, message = "Branch ID is required" });
                }

                var branch = await db.Branches.Where(x => x.BranchId == obj.BranchId).FirstOrDefaultAsync();
                if (branch == null)
                {
                    return BadRequest(new { statusCode = 400, message = "Branch not found" });
                }

                branch.BranchName = obj.BranchName;
                branch.Latitude = obj.Latitude;
                branch.Longitude = obj.Longitude;
                branch.UpdatedAt = DateTime.Now;
                branch.Radius = obj.Radius;

                await db.SaveChangesAsync();
                return Ok(new { statusCode = 200, message = "Branch updated successfully" });
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        [HttpGet("GetBranches")]
        public async Task<IActionResult> GetBranches()
        {
            try
            {
                var branches = await db.Branches.Include(x => x.CreatedByNavigation).Where(x=>x.IsActive == true).Select(x => new
                {
                    x.BranchId,
                    x.BranchName,
                    x.CreatedByNavigation.UserName,
                    x.CreatedAt,
                    x.Latitude,
                    x.Longitude,
                    x.IsActive,
                    x.Radius,
                    TotalCount = x.EmpUserBrDeptMappings.Count(m => (m.EmployeeId != null && m.Employee.IsActive == true) || (m.UserId != null && m.User.IsActive == true))
                }).ToListAsync();
                return Ok(new { statusCode = 200, branches });
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        [HttpGet("GetBranchById")]
        public async Task<IActionResult> GetBranchById(int branchId)
        {
            try
            {
                var branches = await db.Branches.Where(x => x.BranchId == branchId).Select(x => new
                {
                    x.BranchId,
                    x.BranchName,
                    x.CreatedByNavigation.UserName,
                    x.CreatedBy,
                    x.CreatedAt,
                    x.Latitude,
                    x.Longitude,
                    x.IsActive,
                    x.Radius
                }).FirstOrDefaultAsync();
                return Ok(new { statusCode = 200, branches });
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        [HttpPost("DeActivateBranch")]
        public async Task<IActionResult> DeActivateBranch(int branchId)
        {
            try
            {
                
                var check1 = await db.Departments.Where(x=>x.BranchId == branchId && x.IsActive == true).CountAsync();
                var check2 = await db.EmpUserBrDeptMappings.Where(x=> x.BranchId == branchId && (x.Employee.IsActive == true || x.User.IsActive == true)).CountAsync();

                if(check1 > 0 || check2 > 0)
                {
                    return BadRequest(new { statusCode = 400, message = "Branch cannot be deactivated as it is associated with active departments or employees" });
                }

                var _ = await db.Branches.Where(x => x.BranchId == branchId).FirstOrDefaultAsync();
                _.IsActive = false;

                await db.SaveChangesAsync();
                return Ok(new { statusCode = 200, message = "Branch deactivated successfully" });
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        [HttpGet("GetAllMembersOfBranch")]
        public async Task<IActionResult> GetAllMembersOfBranch(int branchId)
        {
            try
            {
                var empids = await db.EmpUserBrDeptMappings.Where(x => x. BranchId == branchId && x.Employee.IsActive == true).Select(x => x.EmployeeId).ToListAsync();

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
    }
}
