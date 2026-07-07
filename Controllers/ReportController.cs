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
    public class ReportController(ppfdbContext _context, ClaimsService _claims) : Controller
    {
        private readonly ppfdbContext db = _context;
        private readonly ClaimsService claims = _claims;

        [HttpGet("DetailedAttendanceReport")]
        public async Task<IActionResult> DetailedAttendanceReport(int employeeId, DateTime From, DateTime To)
        {
            try
            {
                IQueryable<AttendanceLog> query = (IQueryable<AttendanceLog>)db.AttendanceLogs.AsNoTracking()
                   .Where(x => x.EmployeeId == employeeId
                       && (x.TimeInAt ?? x.TimeInMobile ?? x.TimeInImage) >= From
                       && (x.TimeInAt ?? x.TimeInMobile ?? x.TimeInImage) <= To
                   ).GroupBy(x => (x.TimeInAt ?? x.TimeInMobile ?? x.TimeInImage)!.Value.Date)
                    .ToDictionary(g => g.Key, g => g.First());

                List<DetailedAttendanceReportDto> report = new();
                for (var i = From.Date; i <= To; i.AddDays(1))
                {
                    DetailedAttendanceReportDto _ = new();


                }

                    return Json(true);
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }
    }
}
