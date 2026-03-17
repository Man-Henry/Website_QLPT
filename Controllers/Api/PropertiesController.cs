using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Website_QLPT.Data;
using Website_QLPT.Models;

namespace Website_QLPT.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    public class PropertiesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public PropertiesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/properties
        [HttpGet]
        public async Task<IActionResult> GetProperties()
        {
            var properties = await _context.Properties
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Address,
                    p.Description,
                    TotalRooms = p.Rooms.Count(),
                    AvailableRooms = p.Rooms.Count(r => r.Status == RoomStatus.Available),
                    RentedRooms = p.Rooms.Count(r => r.Status == RoomStatus.Rented),
                    MaintenanceRooms = p.Rooms.Count(r => r.Status == RoomStatus.Maintenance)
                })
                .ToListAsync();

            return Ok(new { count = properties.Count, data = properties });
        }

        // GET: api/properties/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetProperty(int id)
        {
            var property = await _context.Properties
                .Where(p => p.Id == id)
                .Select(p => new
                {
                    Property = new
                    {
                        p.Id,
                        p.Name,
                        p.Address,
                        p.Description,
                        p.Latitude,
                        p.Longitude,
                        TotalRooms = p.Rooms.Count(),
                        AvailableRooms = p.Rooms.Count(r => r.Status == RoomStatus.Available),
                        RentedRooms = p.Rooms.Count(r => r.Status == RoomStatus.Rented),
                        MaintenanceRooms = p.Rooms.Count(r => r.Status == RoomStatus.Maintenance)
                    },
                    Rooms = p.Rooms
                        .OrderBy(r => r.Name)
                        .Select(r => new
                        {
                            r.Id,
                            r.Name,
                            r.Price,
                            r.Area,
                            Status = r.Status.ToString(),
                            Note = r.Note,
                            Images = r.Images
                                .OrderByDescending(img => img.IsThumbnail)
                                .ThenBy(img => img.Id)
                                .Select(img => img.ImagePath)
                                .ToList()
                        })
                        .ToList()
                })
                .FirstOrDefaultAsync();

            if (property == null)
            {
                return NotFound(new { message = "Không tìm thấy khu nhà." });
            }

            return Ok(new { data = property });
        }
    }
}
