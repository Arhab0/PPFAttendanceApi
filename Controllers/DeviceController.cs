using PPFAttendanceApi.Helper;
using PPFAttendanceApi.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Xml.Linq;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace PPFAttendanceApi.Controllers
{
    [Route("api/[controller]")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class DeviceController(ppfdbContext _context, ClaimsService _claims) : Controller
    {
        private readonly ppfdbContext db = _context;
        private readonly ClaimsService claims = _claims;

        [HttpGet("GetDeviceSessions")]
        public async Task<IActionResult> GetDeviceSessions()
        {
            try
            {
                var deviceSessions = await db.DeviceSessions
                                        .Include(x => x.Employee)
                                        .Include(x=>x.User)
                                        .Select(x => new
                                        {
                                            Name = x.EmployeeId != null ? x.Employee.EmployeeName : x.User.UserName,
                                            Email = x.EmployeeId != null ? x.Employee.EmployeeEmail : x.User.UserEmail,
                                            x.DeviceSessionId,
                                            x.DeviceId,
                                            x.DeviceName,
                                            x.DevicePlatform,
                                        })
                                        .ToListAsync();

                return Json(deviceSessions);
            }
            catch (Exception ex)
            {
                return BadRequest(new { statusCode = 500, message = "An error occurred while retrieving device sessions." });
            }
        }

        [HttpPost("RemovePreviousSession")]
        public async Task<IActionResult> RemovePreviousSession(int deviceSessionId)
        {
            await db.Database.BeginTransactionAsync();
            try
            {
                var _ = await db.DeviceSessions.Where(x => x.DeviceSessionId == deviceSessionId).FirstOrDefaultAsync();

                if (_ == null)
                {
                    return BadRequest(new { statusCode = 404, message = "Device session not found." });
                }

                var id = _.EmployeeId == null ? _.UserId : _.EmployeeId;
                await db.ActivityLogs.AddAsync(new()
                {
                    CreatedById = int.Parse(claims["sid"]),
                    CreatedId = id ?? 0,
                    CreatedAt = DateTime.Now,
                    Description = $"Previous session removed, Removed device info ( DeviceName = {_.DeviceName}, DevicePlatform = {_.DevicePlatform}, DeviceId = {_.DeviceId} ).",
                });

                db.DeviceSessions.Remove(_);
                await db.SaveChangesAsync();

                await db.Database.CommitTransactionAsync();
                return Json(new { statusCode = 200, message = "Previous session removed successfully." });
            }
            catch (Exception ex)
            {
                await db.Database.RollbackTransactionAsync();
                return BadRequest(new { statusCode = 500, message = "An error occurred while removing previous session." });
            }
        }
    }
}
