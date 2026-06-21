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
    public class PaymentTypeController(ppfdbContext _context) : Controller
    {
        private readonly ppfdbContext db = _context;

        [HttpPost("AddPaymentType")]
        public async Task<IActionResult> AddPaymentType(string type)
        {
            try
            {
                var check = await db.PaymentTypes.Where(x => x.Type == type).FirstOrDefaultAsync();
                if (check != null)
                {
                    return BadRequest(new { statusCode = 400, message = "Payment type already exists." });
                }

                await db.PaymentTypes.AddAsync(new PaymentType { Type = type, IsActive = true });
                await db.SaveChangesAsync();
                return Ok(new { statusCode = 200, message = "Payment type added successfully." });
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        [HttpGet("GetPaymentTypeById")]
        public async Task<IActionResult> GetPaymentTypeById(int id)
        {
            try
            {
                var paymentType = await db.PaymentTypes.FindAsync(id);
                if (paymentType == null)
                {
                    return NotFound(new { statusCode = 404, message = "Payment type not found." });
                }
                return Ok(paymentType);
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        [HttpGet("GetAllPaymentTypes")]
        public async Task<IActionResult> GetAllPaymentTypes()
        {
            try
            {
                var paymentTypes = await db.PaymentTypes.ToListAsync();
                return Ok(paymentTypes);
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        [HttpPost("UpdatePaymentType")]
        public async Task<IActionResult> UpdatePaymentType(int id, string type)
        {
            try
            {
                var paymentType = await db.PaymentTypes.FindAsync(id);
                if (paymentType == null)
                {
                    return NotFound(new { statusCode = 404, message = "Payment type not found." });
                }
                paymentType.Type = type;
                await db.SaveChangesAsync();
                return Ok(new { statusCode = 200, message = "Payment type updated successfully." });
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        [HttpPost("PaymentTypeStatusChange")]
        public async Task<IActionResult> PaymentTypeStatusChange(int paymentTypeId, bool status)
        {
            try
            {
                if (status == false)
                {
                    var check = await db.Employees.AnyAsync(x => x.PaymentTypeId == paymentTypeId && x.IsActive);

                    if (check)
                    {
                        return BadRequest(new { statusCode = 400, message = "Cannot deactivate this Payment Type because it is assigned to active employees." });
                    }

                    var paymentType = await db.PaymentTypes.FirstOrDefaultAsync(x => x.PaymentTypeId == paymentTypeId);

                    paymentType.IsActive = status;
                    await db.SaveChangesAsync();

                    return Json(new { statusCode = 200, message = "Payment Type deactivated successfully." });
                }

                var paymentTypeActive = await db.PaymentTypes.FirstOrDefaultAsync(x => x.PaymentTypeId == paymentTypeId);

                paymentTypeActive.IsActive = status;
                await db.SaveChangesAsync();

                return Json(new { statusCode = 200, message = "Payment Type activated successfully." });
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }
    }
}