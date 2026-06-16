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
    public class ShiftTypeController(ppfdbContext _context) : Controller
    {
        private readonly ppfdbContext db = _context;

        [HttpPost("AddShiftType")]
        public async Task<IActionResult> AddShiftType(string type)
        {
            try
            {
                var check = await db.ShiftTypes.Where(x => x.Type == type).FirstOrDefaultAsync();
                if (check != null)
                {
                    return BadRequest("Shift type already exists.");
                }

                await db.ShiftTypes.AddAsync(new ShiftType { Type = type, IsActive = true });
                await db.SaveChangesAsync();
                return Ok("Shift type added successfully.");
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        [HttpGet("GetShiftById")]
        public async Task<IActionResult> GetShiftById(int id)
        {
            try
            {
                var shiftType = await db.ShiftTypes.FindAsync(id);
                if (shiftType == null)
                {
                    return NotFound("Shift type not found.");
                }
                return Ok(shiftType);
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        [HttpGet("GetAllShiftTypes")]
        public async Task<IActionResult> GetAllShiftTypes()
        {
            try
            {
                var shiftTypes = await db.ShiftTypes.ToListAsync();
                return Ok(shiftTypes);
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        [HttpPost("UpdateShiftType")]
        public async Task<IActionResult> UpdateShiftType(int id, string type)
        {
            try
            {
                var shiftType = await db.ShiftTypes.FindAsync(id);
                if (shiftType == null)
                {
                    return NotFound("Shift type not found.");
                }
                shiftType.Type = type;
                await db.SaveChangesAsync();
                return Ok("Shift type updated successfully.");
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        [HttpPost("ShiftTypeStatusChange")]
        public async Task<IActionResult> ShiftTypeStatusChange(int shiftTypeId, bool status)
        {
            try
            {
                if (status == false)
                {
                    var check = await db.Employees.Where(x => x.ShiftTypeId == shiftTypeId && x.IsActive == true).AnyAsync();

                    if (check)
                    {
                        return BadRequest(new { statusCode = 400, message = "Cannot deactivate this ShiftType with active employees" });
                    }
                    var _ = await db.ShiftTypes.Where(x => x.ShiftTypeId == shiftTypeId).FirstOrDefaultAsync();
                    _?.IsActive = status;
                    return Json(new { statusCode = 200, message = "ShiftType DeActivated successfully." });
                }

                var __ = await db.ShiftTypes.Where(x => x.ShiftTypeId == shiftTypeId).FirstOrDefaultAsync();
                __?.IsActive = status;

                return Json(new { statusCode = 200, message = "ShiftType Activated successfully." });
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }
    }
}
