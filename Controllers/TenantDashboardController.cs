using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Website_QLPT.Data;
using Website_QLPT.Models;
using Website_QLPT.Services;
using Website_QLPT.ViewModels;

namespace Website_QLPT.Controllers
{
    [Authorize(Roles = "Tenant")]
    public class TenantDashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentTenantService _currentTenantService;

        public TenantDashboardController(ApplicationDbContext context, ICurrentTenantService currentTenantService)
        {
            _context = context;
            _currentTenantService = currentTenantService;
        }

        public async Task<IActionResult> Index()
        {
            var tenant = await _currentTenantService.GetCurrentTenantAsync(User);
            if (tenant == null)
            {
                return View(new TenantDashboardViewModel
                {
                    Message = "Tài khoản của bạn chưa được liên kết với hồ sơ người thuê nào. Vui lòng liên hệ chủ nhà để được gắn tài khoản."
                });
            }

            var activeContracts = await _context.Contracts
                .Include(c => c.Room).ThenInclude(r => r!.Property)
                .Where(c => c.TenantId == tenant.Id && c.Status == ContractStatus.Active)
                .ToListAsync();

            var contractIds = activeContracts.Select(c => c.Id).ToList();
            var invoices = await _context.Invoices
                .Include(i => i.Contract)
                .Where(i => contractIds.Contains(i.ContractId))
                .OrderByDescending(i => i.Year).ThenByDescending(i => i.Month)
                .Take(12)
                .ToListAsync();

            return View(new TenantDashboardViewModel
            {
                Tenant = tenant,
                ActiveContracts = activeContracts,
                RecentInvoices = invoices
            });
        }

        public IActionResult InvoiceDetail(int id)
        {
            return RedirectToAction("Details", "Invoices", new { id });
        }

        public IActionResult MyTickets()
        {
            return RedirectToAction("Index", "MaintenanceTickets");
        }

        public IActionResult CreateTicket()
        {
            return RedirectToAction("Create", "MaintenanceTickets");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateTicket(
            [Bind("Title,Description,Priority,ContractId")] MaintenanceTicket ticket,
            IFormFile? image)
        {
            return RedirectToActionPreserveMethod("Create", "MaintenanceTickets");
        }
    }
}
