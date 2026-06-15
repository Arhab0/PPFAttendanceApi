using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PPFAttendanceApi.Helper;
using PPFAttendanceApi.Models;

namespace PPFAttendanceApi.Controllers
{
    [Route("api/[controller]")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class EmployeeTypeController(ppfdbContext _context) : Controller
    {
        private readonly ppfdbContext db = _context;

        [HttpPost("AddEmployeeType")]
        public async Task<IActionResult> AddEmployeeType(string type)
        {
            try
            {
                var check = await db.EmployeeTypes.Where(x => x.Type == type).FirstOrDefaultAsync();
                if (check != null)
                {
                    return BadRequest("Employee type already exists.");
                }

                await db.EmployeeTypes.AddAsync(new EmployeeType { Type = type });
                await db.SaveChangesAsync();
                return Ok("Employee type added successfully.");
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        [HttpGet("GetEmployeeTypeById")]
        public async Task<IActionResult> GetEmployeeTypeById(int id)
        {
            try
            {
                var employeeType = await db.EmployeeTypes.FindAsync(id);
                if (employeeType == null)
                {
                    return NotFound("Employee type not found.");
                }
                return Ok(employeeType);
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        [HttpGet("GetAllEmployeeTypes")]
        public async Task<IActionResult> GetAllEmployeeTypes()
        {
            try
            {
                var employeeTypes = await db.EmployeeTypes.ToListAsync();
                return Ok(employeeTypes);
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        [HttpPost("UpdateEmployeeType")]
        public async Task<IActionResult> UpdateEmployeeType(int id, string type)
        {
            try
            {
                var employeeType = await db.EmployeeTypes.FindAsync(id);
                if (employeeType == null)
                {
                    return NotFound("Employee type not found.");
                }
                employeeType.Type = type;
                await db.SaveChangesAsync();
                return Ok("Employee type updated successfully.");
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }
    }
}