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
    public class ContractsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ContractsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Contracts
        public async Task<IActionResult> Index(string? search, int? page)
        {
            var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            ViewData["Title"] = "Hợp Đồng";
            var query = _context.Contracts
                .Include(c => c.Room).ThenInclude(r => r!.Property)
                .Include(c => c.Tenant)
                .Where(c => c.Room!.Property!.OwnerId == ownerId)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(c => c.Room!.Name.Contains(search) || 
                                         (c.Tenant != null && c.Tenant.FullName.Contains(search)));
            }
            
            ViewBag.Search = search;
            
            int pageSize = 10;
            int pageNumber = page ?? 1;
            var contracts = await query.OrderByDescending(c => c.CreatedAt).ToListAsync();
            return View(contracts.ToPagedList(pageNumber, pageSize));
        }

        // GET: Contracts/Create
        public async Task<IActionResult> Create()
        {
            var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            ViewData["Title"] = "Tạo Hợp Đồng Mới";
            var availableRooms = await _context.Rooms
                .Include(r => r.Property)
                .Where(r => r.Status == RoomStatus.Available && r.Property!.OwnerId == ownerId)
                .ToListAsync();
            ViewBag.RoomId = new SelectList(availableRooms.Select(r => new { r.Id, Name = $"{r.Property?.Name} - {r.Name} ({r.Price:N0} VND)" }), "Id", "Name");
            ViewBag.TenantId = new SelectList(await _context.Tenants.Where(t => t.OwnerId == ownerId).ToListAsync(), "Id", "FullName");
            return View();
        }

        // POST: Contracts/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("RoomId,TenantId,StartDate,EndDate,DepositAmount,Note")] Contract contract)
        {
            var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (ModelState.IsValid)
            {
                var validRoom = await _context.Rooms.Include(r => r.Property).FirstOrDefaultAsync(r => r.Id == contract.RoomId && r.Property!.OwnerId == ownerId);
                var validTenant = await _context.Tenants.FirstOrDefaultAsync(t => t.Id == contract.TenantId && t.OwnerId == ownerId);

                if (validRoom == null || validTenant == null)
                {
                    ModelState.AddModelError("", "Thông tin phòng hoặc khách thuê không hợp lệ hoặc bạn không có quyền sở hữu.");
                }
                else
                {
                    contract.Status = ContractStatus.Active;
                    contract.CreatedAt = DateTime.Now;
                    _context.Add(contract);

                    // Update room status to Rented
                    validRoom.Status = RoomStatus.Rented;

                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Đã tạo hợp đồng thành công và cập nhật trạng thái phòng!";
                    return RedirectToAction(nameof(Index));
                }
            }

            var availableRooms = await _context.Rooms
                .Include(r => r.Property)
                .Where(r => r.Status == RoomStatus.Available && r.Property!.OwnerId == ownerId)
                .ToListAsync();
            ViewBag.RoomId = new SelectList(availableRooms.Select(r => new { r.Id, Name = $"{r.Property?.Name} - {r.Name}" }), "Id", "Name", contract.RoomId);
            ViewBag.TenantId = new SelectList(await _context.Tenants.Where(t => t.OwnerId == ownerId).ToListAsync(), "Id", "FullName", contract.TenantId);
            ViewData["Title"] = "Tạo Hợp Đồng Mới";
            return View(contract);
        }

        // POST: Terminate contract
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Terminate(int id)
        {
            var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var contract = await _context.Contracts.Include(c => c.Room).ThenInclude(r => r!.Property).FirstOrDefaultAsync(c => c.Id == id && c.Room!.Property!.OwnerId == ownerId);
            if (contract == null) return NotFound();

            contract.Status = ContractStatus.Terminated;
            if (contract.Room != null) contract.Room.Status = RoomStatus.Available;

            _context.AuditLogs.Add(new AuditLog
            {
                OwnerId = ownerId ?? string.Empty,
                ActionType = "Terminate",
                EntityName = "Contract",
                EntityId = contract.Id,
                Details = $"Đã thanh lý hợp đồng phòng {contract.Room?.Name}."
            });

            await _context.SaveChangesAsync();
            TempData["Success"] = "Hợp đồng đã được thanh lý và phòng trở về trạng thái Trống!";
            return RedirectToAction(nameof(Index));
        }

        // GET: Contracts/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var contract = await _context.Contracts
                .Include(c => c.Room).ThenInclude(r => r!.Property)
                .Include(c => c.Tenant)
                .FirstOrDefaultAsync(m => m.Id == id && m.Room!.Property!.OwnerId == ownerId);
            if (contract == null) return NotFound();
            ViewData["Title"] = "Sửa Hợp Đồng";
            return View(contract);
        }

        // POST: Contracts/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,RoomId,TenantId,StartDate,EndDate,DepositAmount,Status,Note,CreatedAt")] Contract contract)
        {
            var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (id != contract.Id) return NotFound();

            var existing = await _context.Contracts
                .Include(c => c.Room).ThenInclude(r => r!.Property)
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id && c.Room!.Property!.OwnerId == ownerId);
            if (existing == null) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    // Nếu trạng thái chuyển sang Terminated/Expired → phòng về trống
                    if (contract.Status != ContractStatus.Active && existing.Status == ContractStatus.Active)
                    {
                        var room = await _context.Rooms.FindAsync(contract.RoomId);
                        if (room != null) room.Status = RoomStatus.Available;
                    }
                    _context.Update(contract);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Đã cập nhật hợp đồng thành công!";
                    return RedirectToAction(nameof(Details), new { id = contract.Id });
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Contracts.Any(e => e.Id == contract.Id)) return NotFound();
                    else throw;
                }
            }
            ViewData["Title"] = "Sửa Hợp Đồng";
            return View(contract);
        }

        // GET: Contracts/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var contract = await _context.Contracts
                .Include(c => c.Room).ThenInclude(r => r!.Property)
                .Include(c => c.Tenant)
                .Include(c => c.Invoices)
                .FirstOrDefaultAsync(m => m.Id == id && m.Room!.Property!.OwnerId == ownerId);
            if (contract == null) return NotFound();
            ViewData["Title"] = "Chi tiết Hợp Đồng";
            return View(contract);
        }
    }
}
