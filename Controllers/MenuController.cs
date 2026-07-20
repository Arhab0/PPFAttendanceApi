using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PPFAttendanceApi.Helper;
using PPFAttendanceApi.Models;
using PPFAttendanceApi.Dto;

namespace zms_be.Controllers
{
    [Route("api/[controller]")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class MenuController(ppfdbContext _db, ClaimsService _service) : Controller
    {
        private readonly ppfdbContext db = _db;
        private readonly ClaimsService claims = _service;

        [HttpPost("CreateMenu")]
        public async Task<IActionResult> CreateMenu(Menu obj)
        {
            if (obj.MenuName == null || obj.MenuName == "" || obj.Path == null || obj.Path == "")
            {
                return BadRequest(new { statusCode = 400, message = "MenuId is required" });
            }

            Menu? existingMenu = await db.Menus.FirstOrDefaultAsync(x => x.MenuName == obj.MenuName);
            if (existingMenu != null)
            {
                return BadRequest(new { statusCode = 400, message = "Menu with this name already exists" });
            }

            Menu menu = new()
            {
                MenuName = obj.MenuName,
                Path = obj.Path,
                ParentId = obj.ParentId,
                SortingOrder = obj.SortingOrder,
                IsActive = obj.IsActive,
                Icon = obj.Icon,
                Description = obj.Description
            };

            await db.Menus.AddAsync(menu);
            await db.SaveChangesAsync();
            return Json(new { menu });
        }

        [HttpGet("GetMenus")]
        public async Task<IActionResult> GetMenus()
        {
            int roleId = int.Parse(claims["roleId"]);
            var menus = await db.RoleMenuMappings
                .AsNoTracking()
                .Where(x => x.RoleId == roleId && (x.View || x.Create || x.Edit || x.Delete))
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
                    x.Menu.IsActive
                })
                .OrderBy(x => x.SortingOrder)
                .ToListAsync();
            return Json(new { menus });
        }

        [HttpPost("UpdateMenu")]
        public async Task<IActionResult> UpdateMenu(Menu obj)
        {
            if (obj == null || obj.MenuId == 0)
            {
                return BadRequest(new { statusCode = 400, message = "Object is Empty" });
            }

            Menu? menu = await db.Menus.FirstOrDefaultAsync(x => x.MenuId == obj.MenuId);
            if (menu == null)
            {
                return NotFound(new { statusCode = 404, message = "No Menu Found" });
            }
            menu.MenuName = obj.MenuName;
            menu.Path = obj.Path;
            menu.ParentId = obj.ParentId;
            menu.SortingOrder = obj.SortingOrder;
            menu.IsActive = obj.IsActive;
            menu.Icon = obj.Icon;
            menu.Description = obj.Description;
            menu.UpdatedAt = DateTime.Now;

            await db.SaveChangesAsync();
            return Json(new { menu });
        }

        [HttpPost("DeleteMenu")]
        public async Task<IActionResult> DeleteMenu(int MenuId)
        {
            await db.Database.BeginTransactionAsync();
            try
            {
                if (MenuId == 0)
                {
                    return BadRequest(new { statusCode = 400, message = "MenuId is required" });
                }
                await db.RoleMenuMappings.Where(x => x.MenuId == MenuId).ExecuteDeleteAsync();
                Menu? menu = await db.Menus.FirstOrDefaultAsync(x => x.MenuId == MenuId);
                if (menu == null)
                {
                    return NotFound(new { statusCode = 404, message = "No Menu Found" });
                }
                db.Menus.Remove(menu);
                await db.SaveChangesAsync();
                await db.Database.CommitTransactionAsync();
                return Json(new { menu });
            }
            catch (Exception ex)
            {
                await db.Database.RollbackTransactionAsync();
                return BadRequest(new { Message = ex.Message });
            }
        }

        [HttpGet("GetMenuById")]
        public async Task<IActionResult> GetMenuById(int MenuId)
        {
            if (MenuId == 0)
            {
                return BadRequest(new { statusCode = 400, message = "MenuId is required" });
            }

            var menu = await db.Menus.Select(x => new
            {
                x.MenuId,
                x.MenuName,
                x.Path,
                x.IsParent,
                x.ParentId,
                x.IsActive,
                x.Description,
                x.Icon,
                x.SortingOrder
            }).FirstOrDefaultAsync(x => x.MenuId == MenuId);
            if (menu == null)
            {
                return NotFound(new { statusCode = 404, message = "No Menu Found" });
            }

            return Json(new { menu });
        }

        [HttpPost("MenuReordering")]
        public async Task<IActionResult> MenuReordering(List<MenuSorting> Menus)
        {
            if (Menus == null || Menus.Count == 0)
            {
                return BadRequest(new { statusCode = 400, message = "Menus list cannot be empty" });
            }

            foreach (MenuSorting menu in Menus)
            {
                Menu? existingMenu = await db.Menus.FirstOrDefaultAsync(x => x.MenuId == menu.MenuId);
                if (existingMenu == null)
                {
                    return NotFound(new { statusCode = 404, message = $"Menu with ID {menu.MenuId} not found" });
                }
                existingMenu.SortingOrder = menu.SortingOrder;
                existingMenu.UpdatedAt = DateTime.Now;
            }

            await db.SaveChangesAsync();
            return Json(new { Message = "Menus reordered successfully" });
        }

        [HttpGet("GetAllMenus")]
        public async Task<IActionResult> GetAllMenus()
        {
            var menus = await db.Menus.Select(x => new { x.MenuId, x.MenuName }).OrderBy(x => x.MenuId).ToListAsync();
            return Json(new { menus });
        }
    }
}
