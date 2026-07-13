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
    public class RoleController(ppfdbContext _context, ClaimsService _claims) : Controller
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

        [HttpPost("AddRole")]
        public async Task<IActionResult> AddRole(string roleName)
        {
            await db.Database.BeginTransactionAsync();
            try
            {

                var check = await db.Roles.Where(x => x.RoleName.Trim().ToLower() == roleName.Trim().ToLower()).AnyAsync();

                if (check)
                {
                    return BadRequest(new { statusCode = 400, message = "Role already exists." });
                }

                await db.Roles.AddAsync(new()
                {
                    RoleName = roleName,
                    CreatedAt = DateTime.Now
                });

                await db.SaveChangesAsync();
                await db.Database.CommitTransactionAsync();
                return Ok(new { statusCode = 200, message = "Role added successfully." });
            }
            catch (Exception e)
            {
                await db.Database.RollbackTransactionAsync();
                return BadRequest(e.Message);
            }
        }

        [HttpGet("GetRoleById")]
        public async Task<IActionResult> GetRoleById(int roleId)
        {
            try
            {
                var role = await db.Roles.Where(x => x.RoleId == roleId).Select(x => new { x.RoleId, x.RoleName, x.CreatedAt }).FirstOrDefaultAsync();
                if (role == null)
                {
                    return NotFound(new { statusCode = 404, message = "Role not found." });
                }
                return Ok(role);
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        [HttpPost("UpdateRole")]
        public async Task<IActionResult> UpdateRole(int roleId, string roleName)
        {
            await db.Database.BeginTransactionAsync();
            try
            {

                var check = await db.Roles.Where(x => x.RoleId != roleId && x.RoleName.Trim().ToLower() == roleName.Trim().ToLower()).AnyAsync();

                if (check)
                {
                    return BadRequest(new { statusCode = 400, message = "Role already exists." });
                }

                var r = await db.Roles.Where(x => x.RoleId == roleId).FirstOrDefaultAsync();
                r.RoleName = roleName;

                await db.SaveChangesAsync();
                await db.Database.CommitTransactionAsync();
                return Ok(new { statusCode = 200, message = "Role updated successfully." });
            }
            catch (Exception e)
            {
                await db.Database.RollbackTransactionAsync();
                return BadRequest(e.Message);
            }
        }
    }
}
