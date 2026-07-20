using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PPFAttendanceApi.Helper;
using PPFAttendanceApi.Models;
using System.ComponentModel;
using PPFAttendanceApi.Dto;

namespace zms_be.Controllers
{
    [Route("api/[controller]")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class MappingController(ppfdbContext _db, ClaimsService _service) : Controller
    {
        private readonly ppfdbContext db = _db;
        private readonly ClaimsService claims = _service;

        [HttpPost("CreateMapping")]
        public async Task<IActionResult> CreateMapping(List<MenuMapping> MenuId, int RoleId)
        {
            if (MenuId == null || MenuId.Count == 0 || RoleId == 0)
            {
                return BadRequest(new { statusCode = 400, message = "All fields are required" });
            }
            await db.RoleMenuMappings.Where(x => x.RoleId == RoleId).ExecuteDeleteAsync();
            List<RoleMenuMapping> menus = [];
            foreach (MenuMapping item in MenuId)
            {
                RoleMenuMapping menu = new()
                {
                    MenuId = item.MenuId,
                    RoleId = RoleId,
                    Create = item.Create,
                    View = item.View,
                    Edit = item.Edit,
                    Delete = item.Delete
                };
                menus.Add(menu);
            }
            await db.RoleMenuMappings.AddRangeAsync(menus);

            await db.SaveChangesAsync();
            var mappings = await db.RoleMenuMappings
                .AsNoTracking()
                .Include(x => x.Menu)
                .Where(x => x.RoleId == RoleId && (x.View || x.Create || x.Edit || x.Delete))
                .Select(x => new
                {
                    x.Menu.MenuId,
                    x.Menu.MenuName,
                    x.Menu.SortingOrder,
                    x.Menu.Path,
                    x.Menu.Description,
                    x.Menu.Icon,
                    x.Menu.IsParent,
                    x.Menu.ParentId,
                    x.Menu.IsActive,
                    x.View,
                    x.Edit,
                    x.Create,
                    x.Delete
                })
                .OrderBy(x => x.SortingOrder)
                .ToListAsync();
            return Json(new { mappings, RoleId });
        }

        [HttpGet("GetMappingsByRoleId")]
        public async Task<IActionResult> GetMappingsByRoleId(int roleId)
        {
            var result = await db.RoleMenuMappings
                .AsNoTracking()
                .Where(x => x.RoleId == roleId)
                .Include(x => x.Menu)
                .Select(x => new { x.Menu.MenuId, x.Menu.MenuName })
                .ToListAsync();
            return Json(new { result });
        }

        [HttpPost("CopyPermissions")]
        public async Task<IActionResult> CopyPermissions(int SRoleId, int DRoleId)
        {
            if (SRoleId == 0 || DRoleId == 0)
            {
                return BadRequest(new { statusCode = 400, message = "All fields are reuired" });
            }
            List<MenuMapping> mappings = await db.RoleMenuMappings
                .Where(x => x.RoleId == SRoleId)
                .Select(x => new MenuMapping()
                {
                    MenuId = x.MenuId,
                    View = x.View,
                    Create = x.Create,
                    Edit = x.Edit,
                    Delete = x.Delete
                }).ToListAsync();

            List<RoleMenuMapping> result = new();
            foreach (MenuMapping mapping in mappings)
            {
                RoleMenuMapping menu = new()
                {
                    MenuId = mapping.MenuId,
                    RoleId = DRoleId,
                    Create = mapping.Create,
                    View = mapping.View,
                    Edit = mapping.Edit,
                    Delete = mapping.Delete
                };
                result.Add(menu);
            }

            await db.RoleMenuMappings.AddRangeAsync(result);

            await db.SaveChangesAsync();
            return Json(new { result });
        }

        [HttpGet("GetAllMenus")]
        public async Task<IActionResult> GetAllMenus()
        {
            var allMenus = await db.Menus.Select(x => new { x.MenuId, x.MenuName }).OrderBy(x => x.MenuId).ToListAsync();
            var mapping = await (
                    from r in db.Roles
                    join rm in db.RoleMenuMappings on r.RoleId equals rm.RoleId into roleMenu
                    from rm in roleMenu.DefaultIfEmpty()
                    group rm by r.RoleId into g
                    select new
                    {
                        RoleId = g.Key,
                        RoleName = db.Roles.Where(x => x.RoleId == g.Key).Select(x => x.RoleName).First(),
                        Menus = db.RoleMenuMappings.Where(x => x.RoleId == g.Key).Select(x => new
                        {
                            x.MenuId,
                            x.View,
                            x.Create,
                            x.Edit,
                            x.Delete
                        }).ToList()
                    }
                ).ToListAsync();

            return Json(new { allMenus, mapping });
        }
    }
}
