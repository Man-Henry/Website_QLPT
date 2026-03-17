using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Website_QLPT.Data;
using Website_QLPT.Models;

using X.PagedList;
using X.PagedList.Extensions;

namespace Website_QLPT.Controllers
{
    [Authorize(Roles = "Admin")]
    public class PropertiesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PropertiesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Properties
        public async Task<IActionResult> Index(int? page)
        {
            var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            ViewData["Title"] = "Khu Nhà / Dãy Trọ";
            
            int pageSize = 10;
            int pageNumber = (page ?? 1);
            
            var properties = await _context.Properties
                .Where(p => p.OwnerId == ownerId)
                .Include(p => p.Rooms)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
                
            var pagedProperties = properties.ToPagedList(pageNumber, pageSize);
                
            return View(pagedProperties);
        }

        // GET: Properties/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var property = await _context.Properties
                .Include(p => p.Rooms)
                    .ThenInclude(r => r.Images)
                .FirstOrDefaultAsync(m => m.Id == id && m.OwnerId == ownerId);
            if (property == null) return NotFound();
            ViewData["Title"] = property.Name;
            return View(property);
        }

        // GET: Properties/Create
        public IActionResult Create()
        {
            ViewData["Title"] = "Thêm Khu Nhà Mới";
            return View(new Property());
        }

        // POST: Properties/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name,Address,Description,Latitude,Longitude")] Property property)
        {
            if (ModelState.IsValid)
            {
                property.OwnerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                property.CreatedAt = DateTime.Now;
                _context.Add(property);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Đã thêm khu nhà \"{property.Name}\" thành công!";
                return RedirectToAction(nameof(Index));
            }
            ViewData["Title"] = "Thêm Khu Nhà Mới";
            return View(property);
        }

        // GET: Properties/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var property = await _context.Properties.FirstOrDefaultAsync(p => p.Id == id && p.OwnerId == ownerId);
            if (property == null) return NotFound();
            ViewData["Title"] = "Sửa: " + property.Name;
            return View(property);
        }

        // POST: Properties/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Address,Description,Latitude,Longitude,CreatedAt")] Property property)
        {
            var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (id != property.Id) return NotFound();
            
            var existingProp = await _context.Properties.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id && p.OwnerId == ownerId);
            if (existingProp == null) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    property.OwnerId = ownerId ?? string.Empty;
                    _context.Update(property);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = $"Đã cập nhật khu nhà \"{property.Name}\"!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Properties.Any(e => e.Id == property.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["Title"] = "Sửa: " + property.Name;
            return View(property);
        }

        // POST: Properties/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var property = await _context.Properties.FirstOrDefaultAsync(p => p.Id == id && p.OwnerId == ownerId);
            if (property != null)
            {
                _context.Properties.Remove(property);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Đã xóa khu nhà \"{property.Name}\"!";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
