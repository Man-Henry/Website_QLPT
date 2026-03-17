using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Website_QLPT.Data;
using Website_QLPT.Models;
using Website_QLPT.Services;
using X.PagedList;
using X.PagedList.Extensions;

namespace Website_QLPT.Controllers
{
    [Authorize(Roles = "Admin,Tenant")]
    public class MaintenanceTicketsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly ICurrentTenantService _currentTenantService;

        public MaintenanceTicketsController(
            ApplicationDbContext context,
            IWebHostEnvironment env,
            ICurrentTenantService currentTenantService)
        {
            _context = context;
            _env = env;
            _currentTenantService = currentTenantService;
        }

        private bool IsAdmin => User.IsInRole("Admin");

        // GET: MaintenanceTickets
        public async Task<IActionResult> Index(int? page, string? statusFilter)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var query = _context.MaintenanceTickets
                .Include(t => t.Contract)
                .ThenInclude(c => c!.Room)
                .ThenInclude(r => r!.Property)
                .Include(t => t.Contract!.Tenant)
                .AsQueryable();

            if (IsAdmin)
            {
                query = query.Where(t => t.Contract!.Room!.Property!.OwnerId == userId);
                ViewData["Layout"] = "_Layout";
            }
            else
            {
                var tenant = await _currentTenantService.GetCurrentTenantAsync(User);
                if (tenant == null)
                {
                    TempData["Error"] = "Tài khoản của bạn chưa được liên kết với hồ sơ người thuê.";
                    return RedirectToAction("Index", "TenantDashboard");
                }

                query = query.Where(t => t.Contract!.TenantId == tenant.Id);
                ViewData["Layout"] = "_TenantLayout";
            }

            if (!string.IsNullOrEmpty(statusFilter) && Enum.TryParse<TicketStatus>(statusFilter, out var status))
            {
                query = query.Where(t => t.Status == status);
            }

            ViewBag.StatusFilter = statusFilter;

            int pageSize = 10;
            int pageNumber = (page ?? 1);
            
            var ticketList = await query.OrderByDescending(t => t.CreatedAt).ToListAsync();
            return View(ticketList.ToPagedList(pageNumber, pageSize));
        }

        // GET: MaintenanceTickets/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var ticket = await _context.MaintenanceTickets
                .Include(t => t.Contract)
                .ThenInclude(c => c!.Room)
                .ThenInclude(r => r!.Property)
                .Include(t => t.Contract!.Tenant)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (ticket == null) return NotFound();

            // Authorization check
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (IsAdmin)
            {
                if (ticket.Contract?.Room?.Property?.OwnerId != userId) return Forbid();
                ViewData["Layout"] = "_Layout";
            }
            else
            {
                var tenant = await _currentTenantService.GetCurrentTenantAsync(User);
                if (tenant == null)
                {
                    TempData["Error"] = "Tài khoản của bạn chưa được liên kết với hồ sơ người thuê.";
                    return RedirectToAction("Index", "TenantDashboard");
                }

                if (ticket.Contract?.TenantId != tenant.Id) return Forbid();
                ViewData["Layout"] = "_TenantLayout";
            }

            return View(ticket);
        }

        // GET: MaintenanceTickets/Create
        [Authorize(Roles = "Tenant")]
        public async Task<IActionResult> Create()
        {
            var tenant = await _currentTenantService.GetCurrentTenantAsync(User);
            if (tenant == null)
            {
                TempData["Error"] = "Tài khoản của bạn chưa được liên kết với hồ sơ người thuê.";
                return RedirectToAction("Index", "TenantDashboard");
            }

            var activeContracts = await _context.Contracts
                .Include(c => c.Room)
                .Where(c => c.TenantId == tenant.Id && c.Status == ContractStatus.Active)
                .Select(c => new { c.Id, RoomName = c.Room!.Name })
                .ToListAsync();

            if (activeContracts.Count == 0)
            {
                TempData["Error"] = "Bạn chưa có hợp đồng thuê đang hiệu lực để gửi phiếu bảo trì.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.ContractId = new SelectList(activeContracts, "Id", "RoomName");
            ViewData["Layout"] = "_TenantLayout";
            return View(new MaintenanceTicket());
        }

        // POST: MaintenanceTickets/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Tenant")]
        public async Task<IActionResult> Create(
            [Bind("Title,Description,Priority,ContractId")] MaintenanceTicket ticket,
            IFormFile? imageFile,
            [FromForm(Name = "image")] IFormFile? legacyImageFile)
        {
            var tenant = await _currentTenantService.GetCurrentTenantAsync(User);
            if (tenant == null)
            {
                TempData["Error"] = "Tài khoản của bạn chưa được liên kết với hồ sơ người thuê.";
                return RedirectToAction("Index", "TenantDashboard");
            }

            // Verify they own the contract
            var ownsContract = await _context.Contracts.AnyAsync(
                c => c.Id == ticket.ContractId
                    && c.TenantId == tenant.Id
                    && c.Status == ContractStatus.Active);

            if (!ownsContract)
            {
                ModelState.AddModelError(nameof(MaintenanceTicket.ContractId), "Hợp đồng đã chọn không hợp lệ.");
            }

            if (ModelState.IsValid)
            {
                var uploadedImage = imageFile ?? legacyImageFile;

                if (uploadedImage != null && uploadedImage.Length > 0)
                {
                    var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "tickets");
                    Directory.CreateDirectory(uploadsFolder);
                    var uniqueFileName = Guid.NewGuid().ToString() + "_" + uploadedImage.FileName;
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await uploadedImage.CopyToAsync(fileStream);
                    }
                    ticket.ImagePath = "/uploads/tickets/" + uniqueFileName;
                }

                ticket.CreatedAt = DateTime.Now;
                ticket.Status = TicketStatus.Open;
                _context.Add(ticket);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Đã gửi báo cáo sự cố thành công!";
                return RedirectToAction(nameof(Index));
            }
            
            var activeContracts = await _context.Contracts.Include(c => c.Room)
                .Where(c => c.TenantId == tenant.Id && c.Status == ContractStatus.Active)
                .Select(c => new { c.Id, RoomName = c.Room!.Name }).ToListAsync();
            ViewBag.ContractId = new SelectList(activeContracts, "Id", "RoomName", ticket.ContractId);
            ViewData["Layout"] = "_TenantLayout";
            return View(ticket);
        }

        // GET: MaintenanceTickets/EditStatus/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> EditStatus(int? id)
        {
            if (id == null) return NotFound();

            var ticket = await _context.MaintenanceTickets
                .Include(t => t.Contract).ThenInclude(c => c!.Room).ThenInclude(r => r!.Property)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (ticket == null) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (ticket.Contract?.Room?.Property?.OwnerId != userId) return Forbid();

            ViewData["Layout"] = "_Layout";
            return View(ticket);
        }

        // POST: MaintenanceTickets/EditStatus/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> EditStatus(int id, TicketStatus status)
        {
            var ticket = await _context.MaintenanceTickets
                .Include(t => t.Contract).ThenInclude(c => c!.Room).ThenInclude(r => r!.Property)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (ticket == null) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (ticket.Contract?.Room?.Property?.OwnerId != userId) return Forbid();

            ticket.Status = status;
            ticket.UpdatedAt = DateTime.Now;
            _context.Update(ticket);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã cập nhật trạng thái sự cố!";
            return RedirectToAction(nameof(Index));
        }
    }
}
