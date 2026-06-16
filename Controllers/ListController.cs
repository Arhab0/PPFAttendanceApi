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
                var roles = await db.Roles.Select(x => new { x.RoleId, x.RoleName, x.CreatedAt }).ToListAsync();
                return Json(roles);
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        [HttpGet("GetDepartments")]
        public async Task<IActionResult> GetDepartments()
        {
            try
            {
                var departments = await db.Departments.Where(x=>x.IsActive).Select(x => new { x.DepartmentId, x.DepartmentName, x.CreatedAt }).ToListAsync();
                return Json(departments);
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
                var paymentTypes = await db.PaymentTypes.Select(x => new { x.PaymentTypeId, x.Type }).ToListAsync();
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
                var shiftTypes = await db.ShiftTypes.Select(x => new { x.ShiftTypeId, x.Type }).ToListAsync();
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
                var employeeTypes = await db.EmployeeTypes.Select(x => new { x.EmployeeTypeId, x.Type }).ToListAsync();
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
                var branches = await db.Branches.Where(x=>x.IsActive == true).Select(x => new { x.BranchId, x.BranchName}).ToListAsync();
                return Json(branches);
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }
    }
}
