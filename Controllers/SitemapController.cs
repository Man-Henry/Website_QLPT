using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Website_QLPT.Data;
using Website_QLPT.Models;
using System.Text;
using System.Xml;

namespace Website_QLPT.Controllers
{
    public class SitemapController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SitemapController(ApplicationDbContext context)
        {
            _context = context;
        }

        [Route("sitemap.xml")]
        [ResponseCache(Duration = 86400)] // Cache 24h
        public async Task<IActionResult> Index()
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host}";

            // Lấy tất cả phòng đang trống
            var availableRooms = await _context.Rooms
                .Where(r => r.Status == RoomStatus.Available)
                .Select(r => new { r.Id, r.CreatedAt })
                .ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");

            // Trang chủ
            sb.AppendLine("  <url>");
            sb.AppendLine($"    <loc>{baseUrl}/</loc>");
            sb.AppendLine($"    <changefreq>daily</changefreq>");
            sb.AppendLine($"    <priority>1.0</priority>");
            sb.AppendLine($"    <lastmod>{DateTime.UtcNow:yyyy-MM-dd}</lastmod>");
            sb.AppendLine("  </url>");

            // Trang tất cả phòng
            sb.AppendLine("  <url>");
            sb.AppendLine($"    <loc>{baseUrl}/Home/AllRooms</loc>");
            sb.AppendLine($"    <changefreq>daily</changefreq>");
            sb.AppendLine($"    <priority>0.9</priority>");
            sb.AppendLine($"    <lastmod>{DateTime.UtcNow:yyyy-MM-dd}</lastmod>");
            sb.AppendLine("  </url>");

            // Từng trang chi tiết phòng
            foreach (var room in availableRooms)
            {
                sb.AppendLine("  <url>");
                sb.AppendLine($"    <loc>{baseUrl}/chi-tiet-phong/{room.Id}</loc>");
                sb.AppendLine($"    <changefreq>weekly</changefreq>");
                sb.AppendLine($"    <priority>0.8</priority>");
                sb.AppendLine($"    <lastmod>{room.CreatedAt:yyyy-MM-dd}</lastmod>");
                sb.AppendLine("  </url>");
            }

            sb.AppendLine("</urlset>");

            return Content(sb.ToString(), "application/xml", Encoding.UTF8);
        }
    }
}
