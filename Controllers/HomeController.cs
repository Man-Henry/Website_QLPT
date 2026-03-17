using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Website_QLPT.Data;
using Website_QLPT.Models;
using X.PagedList;
using X.PagedList.Extensions;

namespace Website_QLPT.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string keyword, decimal? minPrice, decimal? maxPrice)
        {
            ViewData["Title"] = "Trang Chủ - Tìm Phòng Trọ";

            var query = _context.Rooms
                .Include(r => r.Property)
                .Include(r => r.Images)
                .Where(r => r.Status == RoomStatus.Available)
                .AsQueryable();

            if (!string.IsNullOrEmpty(keyword))
            {
                var kw = keyword.ToLower();
                query = query.Where(r => r.Name.ToLower().Contains(kw) ||
                                         (r.Property != null
                                          && (r.Property.Name.ToLower().Contains(kw)
                                              || (r.Property.Address != null && r.Property.Address.ToLower().Contains(kw)))));
            }

            if (minPrice.HasValue) query = query.Where(r => r.Price >= minPrice.Value);
            if (maxPrice.HasValue) query = query.Where(r => r.Price <= maxPrice.Value);

            var availableRooms = await query
                .OrderByDescending(r => r.CreatedAt)
                .Take(12)
                .ToListAsync();

            // Thống kê cho Right Sidebar
            var totalRooms = await _context.Rooms.CountAsync();
            var totalAvailable = await _context.Rooms.CountAsync(r => r.Status == RoomStatus.Available);
            var totalRented = await _context.Rooms.CountAsync(r => r.Status == RoomStatus.Rented);

            // Phòng nổi bật (phòng trống mới nhất có ảnh)
            var featuredRoom = await _context.Rooms
                .Include(r => r.Property)
                .Include(r => r.Images)
                .Where(r => r.Status == RoomStatus.Available && r.Images.Any())
                .OrderByDescending(r => r.CreatedAt)
                .FirstOrDefaultAsync();

            featuredRoom ??= availableRooms.FirstOrDefault();

            // Danh sách khu nhà cho filter
            var properties = await _context.Properties
                .Select(p => p.Name)
                .Distinct()
                .ToListAsync();

            ViewBag.Keyword = keyword;
            ViewBag.MinPrice = minPrice;
            ViewBag.MaxPrice = maxPrice;
            ViewBag.TotalRooms = totalRooms;
            ViewBag.TotalAvailable = totalAvailable;
            ViewBag.TotalRented = totalRented;
            ViewBag.FeaturedRoom = featuredRoom;
            ViewBag.Properties = properties;

            return View(availableRooms);
        }

        public async Task<IActionResult> AllRooms(string? keyword, decimal? minPrice, decimal? maxPrice, int page = 1)
        {
            ViewData["Title"] = "Tất Cả Phòng Trống";
            int pageSize = 12;

            var query = _context.Rooms
                .Include(r => r.Property)
                .Include(r => r.Images)
                .Where(r => r.Status == RoomStatus.Available)
                .AsQueryable();

            if (!string.IsNullOrEmpty(keyword))
            {
                var kw = keyword.ToLower();
                query = query.Where(r => r.Name.ToLower().Contains(kw) ||
                                         (r.Property != null
                                          && (r.Property.Name.ToLower().Contains(kw)
                                              || (r.Property.Address != null && r.Property.Address.ToLower().Contains(kw)))));
            }
            if (minPrice.HasValue) query = query.Where(r => r.Price >= minPrice.Value);
            if (maxPrice.HasValue) query = query.Where(r => r.Price <= maxPrice.Value);

            var count = await query.CountAsync();
            var allRooms = await query.OrderByDescending(r => r.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            ViewBag.Keyword = keyword;
            ViewBag.MinPrice = minPrice;
            ViewBag.MaxPrice = maxPrice;
            ViewBag.TotalCount = count;

            var pagedRooms = new StaticPagedList<Room>(allRooms, page, pageSize, count);
            return View(pagedRooms);
        }

        [Route("chi-tiet-phong/{id}")]
        public async Task<IActionResult> RoomDetails(int id)
        {
            var room = await _context.Rooms
                .Include(r => r.Property)
                .Include(r => r.Images)
                .FirstOrDefaultAsync(r => r.Id == id);
            
            if (room == null) return NotFound();
            
            ViewData["Title"] = room.Name;
            return View(room);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View();
        }
    }
}
