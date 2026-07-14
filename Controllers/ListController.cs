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
    public class ListController(ppfdbContext _context, ClaimsService _claims) : Controller
    {
        private readonly ppfdbContext db = _context;
        private readonly ClaimsService claims = _claims;

        [HttpGet("GetRoles")]
        public async Task<IActionResult> GetRoles()
        {
            try
            {
                var roles = await db.Roles.Select(x => new { value = x.RoleId, label = x.RoleName,x.HasLoginAccess}).ToListAsync();
                return Json(roles);
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        [HttpGet("GetPaymentTypes")]
        public async Task<IActionResult> GetPaymentTypes()
        {
            try
            {
                var paymentTypes = await db.PaymentTypes.Select(x => new { value = x.PaymentTypeId, label = x.Type }).ToListAsync();
                return Json(paymentTypes);
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        [HttpGet("GetShiftTypes")]
        public async Task<IActionResult> GetShiftTypes()
        {
            try
            {
                var shiftTypes = await db.ShiftTypes.Select(x => new { value = x.ShiftTypeId, label = x.Type }).ToListAsync();
                return Json(shiftTypes);    
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        [HttpGet("GetEmployeeTypes")]
        public async Task<IActionResult> GetEmployeeTypes()
        {
            try
            {
                var employeeTypes = await db.EmployeeTypes.Select(x => new { value = x.EmployeeTypeId, label = x.Type }).ToListAsync();
                return Json(employeeTypes);
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
                var branches = await db.Branches.Where(x=>x.IsActive == true).Select(x => new { value = x.BranchId, label = x.BranchName }).ToListAsync();
                return Json(branches);
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        [HttpGet("GetDepartmentByBranchIdForReports")]
        public async Task<IActionResult> GetDepartmentByBranchIdForReports(int branchId)
        {
            try
            {
                var departments = await db.Departments.AsNoTracking()
                    .Where(d => d.IsActive == true && d.EmpUserBrDeptMappings.Any(m => m.BranchId == branchId))
                    .Select(d => new { d.DepartmentId, d.DepartmentName })
                    .Distinct()
                    .ToListAsync();
                return Json(departments);
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }
    }
}
