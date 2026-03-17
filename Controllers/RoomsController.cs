using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Website_QLPT.Data;
using Website_QLPT.Models;

using X.PagedList;
using X.PagedList.Extensions;

namespace Website_QLPT.Controllers
{
    [Authorize(Roles = "Admin")]
    public class RoomsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public RoomsController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // GET: Rooms
        public async Task<IActionResult> Index(string? statusFilter, decimal? minPrice, decimal? maxPrice, int? propertyId, int? page)
        {
            var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            ViewData["Title"] = "Danh Sách Phòng";

            var query = _context.Rooms
                .Include(r => r.Property)
                .Include(r => r.Images)
                .Where(r => r.Property!.OwnerId == ownerId)
                .AsQueryable();

            // Filter by status
            if (statusFilter != null && Enum.TryParse<RoomStatus>(statusFilter, out var status))
                query = query.Where(r => r.Status == status);

            // Filter by price range
            if (minPrice.HasValue) query = query.Where(r => r.Price >= minPrice.Value);
            if (maxPrice.HasValue) query = query.Where(r => r.Price <= maxPrice.Value);

            // Filter by property
            if (propertyId.HasValue) query = query.Where(r => r.PropertyId == propertyId.Value);

            int pageSize = 10;
            int pageNumber = (page ?? 1);
            var roomsList = await query.OrderBy(r => r.PropertyId).ThenBy(r => r.Name).ToListAsync();
            var rooms = roomsList.ToPagedList(pageNumber, pageSize);

            ViewBag.Properties = new SelectList(await _context.Properties.Where(p => p.OwnerId == ownerId).ToListAsync(), "Id", "Name", propertyId);
            ViewBag.StatusFilter = statusFilter;
            ViewBag.MinPrice = minPrice;
            ViewBag.MaxPrice = maxPrice;
            ViewBag.PropertyId = propertyId;

            return View(rooms);
        }

        // GET: Rooms/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var room = await _context.Rooms
                .Include(r => r.Property)
                .Include(r => r.Images)
                .Include(r => r.Contracts.Where(c => c.Status == ContractStatus.Active))
                    .ThenInclude(c => c.Tenant)
                .FirstOrDefaultAsync(m => m.Id == id && m.Property!.OwnerId == ownerId);
            if (room == null) return NotFound();
            ViewData["Title"] = room.Name;
            return View(room);
        }

        // GET: Rooms/Create
        public async Task<IActionResult> Create()
        {
            var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            ViewData["Title"] = "Thêm Phòng Mới";
            ViewBag.PropertyId = new SelectList(await _context.Properties.Where(p => p.OwnerId == ownerId).ToListAsync(), "Id", "Name");
            return View();
        }

        // POST: Rooms/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,Area,Price,Status,Note,PropertyId")] Room room, IFormFileCollection? images, int[]? thumbnailIndex)
        {
            var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (ModelState.IsValid)
            {
                var validProperty = await _context.Properties.FirstOrDefaultAsync(p => p.Id == room.PropertyId && p.OwnerId == ownerId);
                if (validProperty == null)
                {
                    ModelState.AddModelError("PropertyId", "Khu nhà không hợp lệ hoặc bạn không có quyền sở hữu.");
                }
                else
                {
                    room.CreatedAt = DateTime.Now;
                    _context.Add(room);
                    await _context.SaveChangesAsync();

                    // Handle image upload
                    if (images != null && images.Count > 0)
                        await SaveRoomImages(room.Id, images, thumbnailIndex);

                    TempData["Success"] = $"Đã thêm phòng \"{room.Name}\" thành công!";
                    return RedirectToAction(nameof(Index));
                }
            }
            ViewBag.PropertyId = new SelectList(await _context.Properties.Where(p => p.OwnerId == ownerId).ToListAsync(), "Id", "Name", room.PropertyId);
            ViewData["Title"] = "Thêm Phòng Mới";
            return View(room);
        }

        // GET: Rooms/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var room = await _context.Rooms.Include(r => r.Property).Include(r => r.Images).FirstOrDefaultAsync(r => r.Id == id && r.Property!.OwnerId == ownerId);
            if (room == null) return NotFound();
            ViewData["Title"] = "Sửa: " + room.Name;
            ViewBag.PropertyId = new SelectList(await _context.Properties.Where(p => p.OwnerId == ownerId).ToListAsync(), "Id", "Name", room.PropertyId);
            return View(room);
        }

        // POST: Rooms/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Area,Price,Status,Note,PropertyId,CreatedAt")] Room room, IFormFileCollection? newImages, int[]? deleteImageIds)
        {
            var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (id != room.Id) return NotFound();
            
            var existingRoom = await _context.Rooms.Include(r => r.Property).AsNoTracking().FirstOrDefaultAsync(r => r.Id == id && r.Property!.OwnerId == ownerId);
            if (existingRoom == null) return NotFound();

            var validProperty = await _context.Properties.FirstOrDefaultAsync(p => p.Id == room.PropertyId && p.OwnerId == ownerId);
            if (validProperty == null) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    // Delete selected images
                    if (deleteImageIds != null && deleteImageIds.Length > 0)
                    {
                        var toDelete = await _context.RoomImages
                            .Where(ri => deleteImageIds.Contains(ri.Id) && ri.RoomId == id)
                            .ToListAsync();
                        foreach (var img in toDelete)
                            DeleteImageFile(img.ImagePath);
                        _context.RoomImages.RemoveRange(toDelete);
                    }

                    // Upload new images
                    if (newImages != null && newImages.Count > 0)
                        await SaveRoomImages(id, newImages, null);

                    _context.Update(room);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = $"Đã cập nhật phòng \"{room.Name}\"!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Rooms.Any(e => e.Id == room.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            ViewBag.PropertyId = new SelectList(await _context.Properties.Where(p => p.OwnerId == ownerId).ToListAsync(), "Id", "Name", room.PropertyId);
            ViewData["Title"] = "Sửa: " + room.Name;
            return View(room);
        }

        // POST: Rooms/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var room = await _context.Rooms.Include(r => r.Property).Include(r => r.Images).FirstOrDefaultAsync(r => r.Id == id && r.Property!.OwnerId == ownerId);
            if (room != null)
            {
                // Delete physical image files
                foreach (var img in room.Images)
                    DeleteImageFile(img.ImagePath);

                _context.Rooms.Remove(room);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Đã xóa phòng \"{room.Name}\" và tất cả hình ảnh liên quan!";
            }
            return RedirectToAction(nameof(Index));
        }

        // ============================
        // Helper methods
        // ============================
        private async Task SaveRoomImages(int roomId, IFormFileCollection files, int[]? thumbnailIndex)
        {
            var uploadPath = Path.Combine(_env.WebRootPath, "uploads", "rooms");
            Directory.CreateDirectory(uploadPath);

            for (int i = 0; i < files.Count; i++)
            {
                var file = files[i];
                if (file.Length <= 0) continue;

                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                var allowedExt = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
                if (!allowedExt.Contains(ext)) continue;

                var fileName = $"room_{roomId}_{Guid.NewGuid():N}{ext}";
                var filePath = Path.Combine(uploadPath, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                    await file.CopyToAsync(stream);

                bool isThumbnail = (thumbnailIndex != null && thumbnailIndex.Contains(i));
                _context.RoomImages.Add(new RoomImage
                {
                    RoomId = roomId,
                    ImagePath = $"/uploads/rooms/{fileName}",
                    IsThumbnail = isThumbnail,
                    UploadedAt = DateTime.Now
                });
            }
            await _context.SaveChangesAsync();
        }

        private void DeleteImageFile(string imagePath)
        {
            try
            {
                var fullPath = Path.Combine(_env.WebRootPath, imagePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (System.IO.File.Exists(fullPath))
                    System.IO.File.Delete(fullPath);
            }
            catch { /* ignore file deletion errors */ }
        }
    }
}
