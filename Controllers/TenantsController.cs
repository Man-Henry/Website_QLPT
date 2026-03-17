using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
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
    public class TenantsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public TenantsController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: Tenants
        public async Task<IActionResult> Index(string? search, int? page)
        {
            var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            ViewData["Title"] = "Khách Thuê";
            var query = _context.Tenants
                .Include(t => t.IdentityUser)
                .Where(t => t.OwnerId == ownerId)
                .AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(t => t.FullName.Contains(search) ||
                                         (t.PhoneNumber != null && t.PhoneNumber.Contains(search)) ||
                                         (t.NationalId != null && t.NationalId.Contains(search)) ||
                                         (t.Email != null && t.Email.Contains(search)));
            ViewBag.Search = search;
            
            int pageSize = 10;
            int pageNumber = (page ?? 1);
            var list = await query.OrderByDescending(t => t.CreatedAt).ToListAsync();
            return View(list.ToPagedList(pageNumber, pageSize));
        }

        // GET: Tenants/Create
        public async Task<IActionResult> Create()
        {
            ViewData["Title"] = "Thêm Khách Thuê";
            await PopulateIdentityUsersAsync(null);
            return View(new Tenant());
        }

        // POST: Tenants/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("FullName,NationalId,PhoneNumber,DateOfBirth,HomeTown,Email,IdentityUserId")] Tenant tenant)
        {
            await ValidateIdentityUserLinkAsync(tenant);
            if (ModelState.IsValid)
            {
                tenant.OwnerId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
                tenant.CreatedAt = DateTime.Now;
                _context.Add(tenant);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Đã thêm khách thuê \"{tenant.FullName}\"!";
                return RedirectToAction(nameof(Index));
            }
            await PopulateIdentityUsersAsync(tenant.IdentityUserId);
            ViewData["Title"] = "Thêm Khách Thuê";
            return View(tenant);
        }

        // GET: Tenants/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var tenant = await _context.Tenants
                .Include(t => t.IdentityUser)
                .FirstOrDefaultAsync(t => t.Id == id && t.OwnerId == ownerId);
            if (tenant == null) return NotFound();
            ViewData["Title"] = "Sửa: " + tenant.FullName;
            await PopulateIdentityUsersAsync(tenant.IdentityUserId, tenant.Id);
            return View(tenant);
        }

        // POST: Tenants/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,FullName,NationalId,PhoneNumber,DateOfBirth,HomeTown,Email,CreatedAt,OwnerId,IdentityUserId")] Tenant tenant)
        {
            var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (id != tenant.Id) return NotFound();
            
            // Ensure they are not spoofing another owner's tenant
            var existingTenant = await _context.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id && t.OwnerId == ownerId);
            if (existingTenant == null) return NotFound();

            await ValidateIdentityUserLinkAsync(tenant, tenant.Id);
            if (ModelState.IsValid)
            {
                tenant.OwnerId = ownerId ?? string.Empty;
                _context.Update(tenant);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Đã cập nhật khách thuê \"{tenant.FullName}\"!";
                return RedirectToAction(nameof(Index));
            }
            await PopulateIdentityUsersAsync(tenant.IdentityUserId, tenant.Id);
            ViewData["Title"] = "Sửa: " + tenant.FullName;
            return View(tenant);
        }

        // POST: Tenants/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var tenant = await _context.Tenants.FirstOrDefaultAsync(t => t.Id == id && t.OwnerId == ownerId);
            if (tenant != null)
            {
                _context.Tenants.Remove(tenant);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Đã xóa khách thuê \"{tenant.FullName}\"!";
            }
            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateIdentityUsersAsync(string? selectedUserId, int? currentTenantId = null)
        {
            var tenantUsers = await _userManager.GetUsersInRoleAsync("Tenant");
            var linkedUserIds = await _context.Tenants
                .Where(t => t.IdentityUserId != null && (!currentTenantId.HasValue || t.Id != currentTenantId.Value))
                .Select(t => t.IdentityUserId!)
                .ToListAsync();

            var options = tenantUsers
                .Where(user => !linkedUserIds.Contains(user.Id) || user.Id == selectedUserId)
                .OrderBy(user => user.Email)
                .Select(user => new SelectListItem
                {
                    Value = user.Id,
                    Text = string.IsNullOrWhiteSpace(user.Email)
                        ? user.UserName ?? user.Id
                        : $"{user.Email}{(user.EmailConfirmed ? string.Empty : " (chưa xác minh)")}"
                })
                .ToList();

            ViewBag.IdentityUserId = new SelectList(options, "Value", "Text", selectedUserId);
        }

        private async Task ValidateIdentityUserLinkAsync(Tenant tenant, int? currentTenantId = null)
        {
            if (string.IsNullOrWhiteSpace(tenant.IdentityUserId))
            {
                tenant.IdentityUserId = null;
                return;
            }

            var identityUser = await _userManager.FindByIdAsync(tenant.IdentityUserId);
            if (identityUser == null || !await _userManager.IsInRoleAsync(identityUser, "Tenant"))
            {
                ModelState.AddModelError(nameof(Tenant.IdentityUserId), "Tài khoản được chọn không phải tài khoản người thuê hợp lệ.");
                return;
            }

            var linkedTenantId = await _context.Tenants
                .Where(t => t.IdentityUserId == tenant.IdentityUserId)
                .Select(t => (int?)t.Id)
                .FirstOrDefaultAsync();

            if (linkedTenantId.HasValue && linkedTenantId.Value != currentTenantId)
            {
                ModelState.AddModelError(nameof(Tenant.IdentityUserId), "Tài khoản này đã được liên kết với hồ sơ khách thuê khác.");
                return;
            }

            if (string.IsNullOrWhiteSpace(tenant.Email))
            {
                tenant.Email = identityUser.Email;
            }
        }
    }
}
